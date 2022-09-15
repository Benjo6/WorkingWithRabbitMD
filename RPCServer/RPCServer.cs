using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

class RPCServer
{
    public static void Main()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            channel.QueueDeclare(
                queue: "rpc_queue",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            channel.BasicQos(0, 1, false);

            var consumer = new EventingBasicConsumer(channel);
            channel.BasicConsume(
                queue: "rpc_queue",
                autoAck: false,
                consumer: consumer);
            Console.WriteLine(" [x] Awaiting RPC requests");

            consumer.Received += (model, ea) =>
            {
                string response = null;
                var body = ea.Body.ToArray();
                var properties = ea.BasicProperties;
                var replyProperties = channel.CreateBasicProperties();
                replyProperties.CorrelationId = properties.CorrelationId;

                try
                {
                    var message = Encoding.UTF8.GetString(body);
                    int n = int.Parse(message);
                    Console.WriteLine(" [.] fibonacci({0})",message);
                    response = Fibonacci(n).ToString();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(" [.] ",ex.Message);
                    response = "";
                }
                finally
                {
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    
                    channel.BasicPublish(
                        exchange: "",
                        routingKey: properties.ReplyTo,
                        basicProperties: replyProperties,
                        body: responseBytes);
                    
                    channel.BasicAck(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false);
                }
            };
            Console.WriteLine(" Press [enter] to exit");
            Console.ReadLine();
        }
    }

    private static int Fibonacci(int n)
    {
        if (n == 0 || n == 1)
        {
            return n;
        }
        return Fibonacci(n-1)+Fibonacci(n-2);
    }
}