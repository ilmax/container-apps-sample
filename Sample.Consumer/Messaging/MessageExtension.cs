using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;

namespace Sample.Consumer.Messaging;

public static class MessageExtension
{
    private const string CorrelationContextPropertyName = "Correlation-Context";

    internal static void InstrumentActivityFromMessage(this ServiceBusReceivedMessage message)
    {
        var currentActivity = Activity.Current;
        if (currentActivity is not null)
        {
            if (message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var diagnosticId) && diagnosticId is string parentActivityId)
            {
                currentActivity.SetParentId(parentActivityId);
            }

            if (message.TryExtractCorrelationContext(out var baggageContext))
            {
                foreach (var keyValuePair in baggageContext)
                {
                    currentActivity.AddBaggage(keyValuePair.Key, keyValuePair.Value);
                }
            }
        }
    }

    internal static void InjectActivityBaggage(this ServiceBusMessage message)
    {
        var currentActivity = Activity.Current;
        if (currentActivity?.Baggage is not null)
        {
            message.ApplicationProperties[CorrelationContextPropertyName] = string.Join(",", currentActivity.Baggage.Select(kvp => kvp.Key + "=" + kvp.Value));
        }
    }

    private static bool TryExtractCorrelationContext(this ServiceBusReceivedMessage message, [NotNullWhen(true)] out IList<KeyValuePair<string, string>>? context)
    {
        context = null;
        try
        {
            if (message.ApplicationProperties.TryGetValue(CorrelationContextPropertyName, out var ctxObj))
            {
                string? ctxStr = ctxObj as string;
                if (string.IsNullOrEmpty(ctxStr))
                {
                    return false;
                }

                var ctxList = ctxStr.Split(',');
                if (ctxList.Length == 0)
                {
                    return false;
                }

                context = new List<KeyValuePair<string, string>>(ctxList.Length);
                foreach (string item in ctxList)
                {
                    var readOnlySpan = item.AsSpan();
                    var index = readOnlySpan.IndexOf('=');
                    if (index > 0)
                    {
                        context.Add(new KeyValuePair<string, string>(item.AsSpan(0, index).ToString(), item.AsSpan(index + 1).ToString()));
                    }
                }

                return true;
            }
        }
        catch (Exception)
        {
            // ignored, if context is invalid, there nothing we can do:
            // invalid context was created by consumer, but if we throw here, it will break message processing on producer
            // and producer does not control which context it receives
        }
        return false;
    }
}