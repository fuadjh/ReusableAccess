using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReusableAccess.Application.Completion;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Infrastructure.Completion;

public sealed class AccountMessageSender(
    IConfiguration config,
    IHttpClientFactory clients) : IAccountMessageSender
{
    public bool IsConfigured(DeliveryChannel channel) => channel switch
    {
        DeliveryChannel.Email =>
            !string.IsNullOrWhiteSpace(config["Delivery:Smtp:Host"]) &&
            !string.IsNullOrWhiteSpace(config["Delivery:Smtp:From"]),

        DeliveryChannel.Sms =>
            Uri.TryCreate(config["Delivery:Sms:Endpoint"],
                UriKind.Absolute, out var uri) &&
            uri.Scheme == "https" &&
            !string.IsNullOrWhiteSpace(config["Delivery:Sms:ApiKey"]),

        _ => false
    };

    public async Task SendAsync(
        DeliveryChannel channel,
        string destination,
        string code,
        CancellationToken ct)
    {
        if (!IsConfigured(channel))
            throw new CompletionException(503, "سرویس ارسال پیکربندی نشده است.");

        var text = $"کد تأیید شما: {code}\nاعتبار: ۵ دقیقه. کد را به دیگران ندهید.";

        if (channel == DeliveryChannel.Email)
        {
            using var smtp = new SmtpClient(
                config["Delivery:Smtp:Host"],
                config.GetValue("Delivery:Smtp:Port", 587))
            {
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(config["Delivery:Smtp:User"]))
            {
                smtp.Credentials = new NetworkCredential(
                    config["Delivery:Smtp:User"],
                    config["Delivery:Smtp:Password"]);
            }

            using var mail = new MailMessage(
                config["Delivery:Smtp:From"]!, destination,
                "کد تأیید حساب", text);

            await smtp.SendMailAsync(mail, ct);
            return;
        }

        // قرارداد gateway: POST { to, message } با هدر X-Api-Key.
        using var request = new HttpRequestMessage(
            HttpMethod.Post, config["Delivery:Sms:Endpoint"]);

        request.Headers.Add("X-Api-Key", config["Delivery:Sms:ApiKey"]);
        request.Content = JsonContent.Create(new
        {
            to = destination,
            message = text
        });

        using var response = await clients.CreateClient("AccountDelivery")
            .SendAsync(request, ct);

        response.EnsureSuccessStatusCode();
    }
}

public sealed class AccountDeliveryQueue(
    IServiceScopeFactory scopes,
    ILogger<AccountDeliveryQueue> logger)
    : BackgroundService, IRecoveryCodeQueue
{
    private sealed record Entry(
        DeliveryChannel Channel, string Destination, string Code);

    private readonly Channel<Entry> _queue =
        Channel.CreateBounded<Entry>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(
        DeliveryChannel channel, string destination, string code)
        => _queue.Writer.TryWrite(new(channel, destination, code));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();

                    var sender = scope.ServiceProvider
                        .GetRequiredService<IAccountMessageSender>();

                    await sender.SendAsync(
                        item.Channel,
                        item.Destination,
                        item.Code,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    // نه مقصد و نه کد در log قرار نمی‌گیرند.
                    logger.LogWarning(
                        "Recovery delivery failed for channel {Channel}.",
                        item.Channel);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // لغو خواندن کانال هنگام توقف Host طبیعی است.
        }
    }
}