### Пример 1
```C#
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            string connectionString = new SqlConnectionStringBuilder
            {
                ApplicationName = "ConsoleApp1",
                DataSource = "localhost",
                InitialCatalog = "test1",
                IntegratedSecurity = true,
                ConnectTimeout = 60
            }.ConnectionString;

            var conId = new HashSet<Guid>(200);
            var tasks = new List<Task>(200);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < 200; i++)
            {
                int j = i;
                tasks.Add
                (
                    Task.Run
                    (
                        async () =>
                        {
                            using (var con = new SqlConnection(connectionString))
                            {
                                await con.OpenAsync();
                                bool fromPool;
                                lock(conId)
                                {
                                    fromPool = !conId.Add(con.ClientConnectionId);
                                }
                                WriteLine($"Connection opened {j} {con.ClientConnectionId}. From pool: {fromPool}.  ManagedThreadId: {Thread.CurrentThread.ManagedThreadId}. {stopwatch.ElapsedMilliseconds} ms elapsed.");
                                Thread.Sleep(5000);
                            }
                        }
                    )
                );
            }
            Task.WaitAll(tasks.ToArray());
            WriteLine($"Distinct connections {conId.Distinct().Count()}");
        }
    }
}
```
### Output:
```
...
Connection opened 99 c850d380-925d-435d-9ad2-98b0a0b0bd0a. From pool: False.  ManagedThreadId: 26. 27038 ms elapsed.
Connection opened 98 caf71e01-c127-4edf-b57f-37e1870eca7e. From pool: False.  ManagedThreadId: 30. 27038 ms elapsed.
Connection opened 100 9dbf0c54-4503-48f0-9e2a-a74730ab2e11. From pool: False.  ManagedThreadId: 18. 27053 ms elapsed.
Connection opened 101 2c7ea3da-1612-4ee5-a01a-3ff748866066. From pool: True.  ManagedThreadId: 13. 27567 ms elapsed.
Connection opened 102 e219c056-fdc5-4472-b5a1-ecc335f07836. From pool: True.  ManagedThreadId: 35. 28522 ms elapsed.
Connection opened 103 e1356239-1bae-4b4a-8960-c490ea41b77e. From pool: True.  ManagedThreadId: 27. 28532 ms elapsed.
Connection opened 104 ce1c9d02-ec1e-4020-8f78-66911c44a69b. From pool: True.  ManagedThreadId: 31. 28532 ms elapsed.
Connection opened 105 7263dc8d-4d6f-4e29-8188-67c636d0e0ea. From pool: True.  ManagedThreadId: 23. 28547 ms elapsed.
...
Connection opened 197 3268b4b5-acb9-4f0d-97e8-915880563d38. From pool: True.  ManagedThreadId: 7. 41369 ms elapsed.
Connection opened 198 36921e20-98cd-4d23-85dd-c14d07135924. From pool: True.  ManagedThreadId: 12. 41385 ms elapsed.
Connection opened 199 caf71e01-c127-4edf-b57f-37e1870eca7e. From pool: True.  ManagedThreadId: 42. 42026 ms elapsed.
Connection opened 144 c850d380-925d-435d-9ad2-98b0a0b0bd0a. From pool: True.  ManagedThreadId: 38. 42041 ms elapsed.
Connection opened 142 9dbf0c54-4503-48f0-9e2a-a74730ab2e11. From pool: True.  ManagedThreadId: 34. 42057 ms elapsed.
Distinct connections 100
```
По умолчанию в ADO.NET пул соединений включен (```Pooling=true```), поэтому при закрытии очередного соединения с DBMS, данное соединение помещается в пул соединений, а не удаляется. По умолчанию размер пула равен 100 (```MaxPoolSize=100```). Также отметим, что строка подключения и пользователь, из-под которого запускается программа, не изменяются, а значит все создаваемые подключения в приведенном выше сниппете считаются идентичными.

На первой итерации цикла в пуле отсутствуют какие-либо соединения, поэтому создается новое (```con.OpenAsync()```). Через 5 секунд данное подключение **не закрывается, а помещается в пул соединений**.

На последующих итерациях цикла выполняются аналогичные действия. Обратите внимание, мы не ожидаем сразу же завершения очередного созданного потока (```Task.Run()```), поэтому новые подключения создаются очень быстро один за другим, а пауза в 5 секунд перед "закрытием" соединения (= перед помещением соединения в пул) позволяет отсрочить добавление очередного соединения в пул. Поэтому в консоли мы видим создание новых первых 100 подключений, которые заполняют пул.

На 101 итерации цикла пул заполнен и свободные подключения отсутствуют, поэтому 101 запрос на подключение к DBMS помещается в очередь. Время ожидания в очереди задается параметром ConnectTimeout в секундах (```ConnectTimeout=60```). Т.к. задержка перед закрытием соединений всего лишь 5 секунд, то таски из первых 100 потоков успевают завершить свою работу в течение 60 секунд и соответствующие соединения отправляются в пул. Поэтому начиная со 101 итерации новые подключения не создаются, а берутся из пула.

В итоге в консоли мы видим, что было создано 100 разных подключений, что эквивалентно размеру пула.

If the maximum pool size has been reached and no usable connection is available, the request is queued. The pooler then tries to reclaim any connections until the time-out is reached (the default is 15 seconds). If the pooler cannot satisfy the request before the connection times out, an exception is thrown.
