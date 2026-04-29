# MQTTnet v4.3.x Connection Summary

**Package Version:** `MQTTnet 4.3.2.930`

## Key Namespaces

```csharp
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
```

---

## 1. Creating the MQTT Client

```csharp
var factory = new MqttFactory();
IMqttClient client = factory.CreateMqttClient();
```

- Use `MqttFactory` to create instances of `IMqttClient`
- The client is **not** thread-safe for connection operations; only one connection should be active at a time

---

## 2. Building Connection Options

Use `MqttClientOptionsBuilder` (fluent API):

```csharp
var options = new MqttClientOptionsBuilder()
    .WithTcpServer("broker.example.com", 1883)  // Host and port
    .WithClientId("my-client-id")                // Unique client identifier
    .WithCredentials("username", "password")     // Optional authentication
    .WithWillTopic("status/topic")               // Last Will topic
    .WithWillPayload("offline")                  // Last Will payload
    .Build();
```

**Important Notes:**
- Strip protocol prefixes (e.g., `mqtt://`) from the broker URL before passing to `WithTcpServer()`
- Default port is `1883` for non-TLS, `8883` for TLS
- `ClientId` should be unique per connection to avoid session conflicts

---

## 3. Connecting to the Broker

```csharp
await client.ConnectAsync(options, cancellationToken);
```

- Returns `MqttClientConnectResult` (can be ignored for basic use)
- Check `client.IsConnected` property to verify connection state
- **Throws:**
  - `MqttCommunicationException` – network/protocol errors
  - `SocketException` – underlying TCP connection failures

---

## 4. Recommended Retry Strategy (Exponential Backoff)

```csharp
const int maxAttempts = 5;
var delay = TimeSpan.FromSeconds(1);
var maxDelay = TimeSpan.FromSeconds(30);

for (int attempt = 1; attempt <= maxAttempts; attempt++)
{
    try
    {
        await client.ConnectAsync(options, cancellationToken);
        if (client.IsConnected) break;
    }
    catch (SocketException) { /* log and retry */ }
    catch (MqttCommunicationException) { /* log and retry */ }

    // Exponential backoff with jitter
    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.2)));
    await Task.Delay(delay + jitter, cancellationToken);
    delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, maxDelay.TotalMilliseconds));
}
```

---

## 5. Publishing Messages

```csharp
var message = new MqttApplicationMessageBuilder()
    .WithTopic("my/topic")
    .WithPayload("message content")
    .Build();

await client.PublishAsync(message, cancellationToken);
```

- Always check `client.IsConnected` before publishing
- Payload can be `string`, `byte[]`, or stream

---

## 6. Disconnecting

```csharp
if (client.IsConnected)
{
    await client.DisconnectAsync();
}
```

- Optionally publish an "offline" status message before disconnecting
- The Last Will message is sent automatically by the broker if the client disconnects unexpectedly

---

## 7. Configuration Model

```csharp
public class MqttSettings
{
    public string? BrokerUrl { get; set; }        // e.g., "mqtt://localhost" or "localhost"
    public int BrokerPort { get; set; } = 1883;
    public string? ClientId { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string BaseTopic { get; set; } = "systems-one";
}
```

---

## 8. Exception Handling

| Exception Type | When It Occurs |
|----------------|----------------|
| `SocketException` | TCP connection failure (broker unreachable) |
| `MqttCommunicationException` | Protocol-level communication error |
| `OperationCanceledException` | Cancellation token triggered |

---

## 9. Best Practices

1. **Use `using` scopes or dispose** the client when done
2. **Implement retry logic** with exponential backoff for resilience
3. **Configure Last Will** for status tracking (broker publishes if client drops unexpectedly)
4. **Publish online status** immediately after successful connection
5. **Publish offline status** before graceful disconnect
6. **Always check `IsConnected`** before publishing

---

This summary covers MQTTnet v4.3.x patterns. The key classes are:
- `MqttFactory` - Creates client instances
- `IMqttClient` - The MQTT client interface
- `MqttClientOptionsBuilder` - Builds connection options
- `MqttApplicationMessageBuilder` - Builds messages to publish
