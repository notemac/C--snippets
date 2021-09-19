Рассмотри
```C# {.line-numbers}
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using static System.Console;

namespace ConsoleApp1
{
    class Program
    {
        static async Task Main(string[] args)  
        {
            var conBuilder = new SqlConnectionStringBuilder
            {
                ApplicationName = "ConsoleApp1",
                DataSource = "localhost",
                InitialCatalog = "test1",
                IntegratedSecurity = true
            };
            var connectionsId = new List<Guid>(200);
            var tasks = new List<Task>(200);
            for (int i = 0; i < 200; i++)
            {
                Task t = Task.Run
                (
                    async () =>
                    {
                        using (var con = new SqlConnection(conBuilder.ConnectionString))
                        {
                            await con.OpenAsync();
                            Thread.Sleep(500);
                            connectionsId.Add(con.ClientConnectionId);
                            WriteLine($"Connection opened {i} {con.ClientConnectionId}");
                        }
                    }
                );
                tasks.Add(t);
            }
            Task.WaitAll(tasks.ToArray());
            WriteLine(connectionsId.Distinct().Count());
        }
    }
}
```
