using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPCClient
{
    public class Program
    {
        public static void Main()
        {
            var client = new Client();

            Console.WriteLine(" [x] Requesting Fibonacci(30)");
            var response = client.Call("30");

            Console.WriteLine(" [.] Got '{0}'",response);
            client.Close();
        }
    }
}
