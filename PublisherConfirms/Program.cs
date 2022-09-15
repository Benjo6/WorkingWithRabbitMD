using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

class Program
{
    private const int MESSAGE_COUNT = 50_000;

    public static void Main()
    {
        PublishMessagesIndividually();
        PublishMessageInBatch();
        HandlePublishConfirmsAsync();
    }
    private static IConnection CreateConnection()
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        return factory.CreateConnection();
    }

    private static void HandlePublishConfirmsAsync()
    {
        using (var connection = CreateConnection())
        using (var channel = connection.CreateModel())
        {
            //declare a server named queue
            var queueName = channel.QueueDeclare(queue: "").QueueName;
            channel.ConfirmSelect();

            var outstandingConfirms = new ConcurrentDictionary<ulong, string>();

            void cleanOutstandingConfirms(ulong sequenceNumber, bool multiple)
            {
                if (multiple)
                {
                    var confirmed = outstandingConfirms.Where(k => k.Key <= sequenceNumber);
                    foreach (var entry in confirmed)
                    {
                        outstandingConfirms.TryRemove(entry.Key, out _);
                    }
                }
                else
                    outstandingConfirms.TryRemove(sequenceNumber, out _);
            }
            channel.BasicAcks += (sender, ea) => cleanOutstandingConfirms(ea.DeliveryTag, ea.Multiple);
            channel.BasicNacks += (sender, ea) =>
            {
                outstandingConfirms.TryGetValue(ea.DeliveryTag, out string body);
                Console.WriteLine($"Message with body {body} has been nack-ed. Sequence number: {ea.DeliveryTag}, multiple: {ea.Multiple}");
                cleanOutstandingConfirms(ea.DeliveryTag, ea.Multiple);
            };

            var timer = new Stopwatch();
            timer.Start();
            for (int i = 0; i < MESSAGE_COUNT; i++)
            {
                var body = i.ToString();
                outstandingConfirms.TryAdd(channel.NextPublishSeqNo, i.ToString());
                channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: Encoding.UTF8.GetBytes(body));
            }

            if (!WaitUntil(60, () => outstandingConfirms.IsEmpty))
                throw new Exception("All messages could not be confirmed in 60 seconds");

            timer.Stop();
            Console.WriteLine($"Published {MESSAGE_COUNT:N0} messages and handled confirm async {timer.ElapsedMilliseconds:N0} ms");
        }
    }

    private static bool WaitUntil(int numberOfSeconds, Func<bool> condition)
    {
        int waited = 0;
        while (!condition()&&waited<numberOfSeconds*1000)
        {
            Thread.Sleep(100);
            waited += 100;
        }
        return condition();
    }

    private static void PublishMessageInBatch()
    {
        using (var connection = CreateConnection())
        using (var channel = connection.CreateModel())
        {
            var queueName = channel.QueueDeclare(queue: "").QueueName;
            channel.ConfirmSelect();

            var batchSize = 100;
            var outstandinggMessageCount = 0;
            var timer = new Stopwatch();
            timer.Start();
            for (int i = 0; i < MESSAGE_COUNT; i++)
            {
                var body = Encoding.UTF8.GetBytes(i.ToString());
                channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: body);
                outstandinggMessageCount++;

                if (outstandinggMessageCount == batchSize)
                {
                    channel.WaitForConfirmsOrDie(new TimeSpan(0, 0,5));
                    outstandinggMessageCount = 0;
                }
            }
            if (outstandinggMessageCount > 0)
                channel.WaitForConfirmsOrDie(new TimeSpan(0, 0,5));

            timer.Stop();
            Console.WriteLine($"Published {MESSAGE_COUNT:N0} messages in batch in {timer.ElapsedMilliseconds:N0} ms");
        }
    }

    private static void PublishMessagesIndividually()
    {
        using(var connection = CreateConnection())
        using(var channel = connection.CreateModel())
        {
            //declare a server named queue
            var queueName = channel.QueueDeclare(queue: "").QueueName;
            channel.ConfirmSelect();

            var timer = new Stopwatch();
            timer.Start();
            for (int i = 0; i < MESSAGE_COUNT; i++)
            {
                var body = Encoding.UTF8.GetBytes(i.ToString());
                channel.BasicPublish(
                    exchange: "", 
                    routingKey: 
                    queueName, 
                    basicProperties: null, 
                    body: body);
                channel.WaitForConfirmsOrDie(new TimeSpan(0, 0, 5));
            }
            timer.Stop();
            Console.WriteLine($"Published {MESSAGE_COUNT:N0} messages individually in {timer.ElapsedMilliseconds:N0} ms");
        }
    }


    

}