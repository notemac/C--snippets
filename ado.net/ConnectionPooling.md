### Пример 1. SQL Server Connection Pooling (ADO.NET)
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
            WriteLine($"Distinct connections {conId.Count}");
        }
    }
}
```
#### Output:
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
По умолчанию в ADO.NET пул соединений включен (```Pooling=true```), поэтому при закрытии очередного соединения с DBMS, данное соединение помещается в пул соединений, а не удаляется. По умолчанию максимальный размер пула равен 100 (```MaxPoolSize=100```). Также отметим, что строка подключения и пользователь, из-под которого запускается программа, не изменяются, а значит все создаваемые подключения в приведенном выше сниппете считаются идентичными.

На первой итерации цикла в пуле отсутствуют какие-либо соединения, поэтому создается новое (```con.OpenAsync()```). Через 5 секунд данное подключение **не закрывается, а помещается в пул соединений**.

На последующих итерациях цикла выполняются аналогичные действия. Обратите внимание, мы не ожидаем сразу же завершения очередного созданного потока (```Task.Run()```), поэтому новые подключения создаются очень быстро один за другим, а пауза в 5 секунд перед "закрытием" соединения (= перед помещением соединения в пул) позволяет отсрочить добавление очередного соединения в пул. Поэтому в консоли мы видим создание новых первых 100 подключений, которые заполняют пул.

На 101 итерации цикла пул заполнен и свободные подключения отсутствуют, поэтому 101 запрос на подключение к DBMS помещается в очередь. Время ожидания в очереди задается параметром ConnectTimeout в секундах (```ConnectTimeout=60```). Т.к. задержка перед закрытием соединений всего лишь 5 секунд, то первые 100 тасков успевают завершить свою работу в течение 60 секунд и соответствующие соединения отправляются в пул. Поэтому начиная со 101 итерации новые подключения не создаются, а берутся из пула.

В итоге в консоли мы видим, что было создано 100 разных подключений, что эквивалентно размеру пула.

Если дожидаться завершения каждого потока (```await Task.Run()```), то будет создано только одно соединение в пуле, которое и будет использоваться в каждом потоке:
```
Connection opened 0 34259171-710d-4b78-9837-04181cd76923. From pool: False.  ManagedThreadId: 5. 1439 ms elapsed.
Connection opened 1 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 4. 1525 ms elapsed.
Connection opened 2 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 4. 1586 ms elapsed.
Connection opened 3 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 4. 1648 ms elapsed.
Connection opened 4 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 4. 1710 ms elapsed.
...
Connection opened 196 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 6. 13740 ms elapsed.
Connection opened 197 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 4. 13803 ms elapsed.
Connection opened 198 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 6. 13865 ms elapsed.
Connection opened 199 34259171-710d-4b78-9837-04181cd76923. From pool: True.  ManagedThreadId: 6. 13927 ms elapsed.
Distinct connections 1
```
Теперь уменьшим время ожидания покдлючения до 15 секунд (```ConnectTimeout=15```). С большой вероятностью возникнут ошибки:
```
...
Connection opened 98 1bb2e420-6dcc-4714-9ea6-dc0abe61892e. From pool: False.  ManagedThreadId: 6. 25441 ms elapsed.
Connection opened 99 c74fb478-0cea-4717-893a-425da185d407. From pool: False.  ManagedThreadId: 32. 26015 ms elapsed.
Connection opened 100 a9123b12-c21e-4d71-882a-4ace2b887508. From pool: True.  ManagedThreadId: 35. 26018 ms elapsed.
Connection opened 101 c9229e78-6b41-4538-acfd-1f05eccce2c7. From pool: True.  ManagedThreadId: 27. 26030 ms elapsed.
Connection opened 102 fc1282f5-02f8-4a68-8e82-5d60c2a9f666. From pool: True.  ManagedThreadId: 18. 26045 ms elapsed.
...
Connection opened 122 3a39a96c-1103-4e4e-ad77-8d9531ca988d. From pool: True.  ManagedThreadId: 22. 30053 ms elapsed.
Connection opened 123 62622b41-c11b-4139-bea3-d2a1cad8b49a. From pool: True.  ManagedThreadId: 5. 30442 ms elapsed.
Connection opened 124 2a5d6d58-bdc9-432e-9eb8-25b1eb5ce488. From pool: True.  ManagedThreadId: 7. 30458 ms elapsed.
Unhandled exception. System.AggregateException: One or more errors occurred. (Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.) (Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.) (Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.) (Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached.)
...
```
Мой ноутбук имеет 2 физических ядра, т.е. реально параллельно могут выполняться только 2 потока. Каждому из 200 тасков выделяется определенный квант процессерного времени, т.е. во время выполнения программы происходит постоянное переключение между потоками, что сказывается на времени выполнения каждого таска. В итоге некоторые из первых 100 тасков не успевают завершиться за 15 секунд, поэтому для некоторых последующих запросов на подключение к DBMS отсутствуют свободные соединения в пуле и через 15 сек бросаются исключения из-за превышения времени ожидания подключения.

**Необходимо уточнить, что при выполнении данного сниппета на самом деле будет создаваться только несколько десятков потоков, т.к. потоки берутся и создаются в Thread Pool'e, которым управляет OS согласно зашитым в нее алгоритмам.**

### Пример 2. Removing Connections
При настройке строки подключения по умолчанию каждое соединение с DBMS из пула соединений удаляется примерно через 4-8 минут в случае неактивности.
```C#
using System.Threading;
using Microsoft.Data.SqlClient;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)  
        {
            string connectionString = new SqlConnectionStringBuilder
            {
                ApplicationName = "ConsoleApp1",
                DataSource = "localhost",
                InitialCatalog = "test1",
                IntegratedSecurity = true
            }.ConnectionString;

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
            }
            Thread.Sleep(600_000);
        }
    }
}

```
Через 4-8 минут можем увидеть, например, с помощью SQL-скрипта, что подключение удалено. Хотя консольное приложение еще работает, ожидания завершения ```Thread.Sleep(600_000)```.
```SQL
select * 
from 
	sys.dm_exec_connections
where 
	session_id in (
		select session_id 
		from 
			sys.dm_exec_sessions 
		where 
			program_name = 'ConsoleApp1'
	)
```
### MinPoolSize
Если пул пустой и создается новое соединение с параметром ```MinPoolSize```, то в пул будет добавлено ```MinPoolSize``` аналогичных соединений, которые не будут удаляться из пула по истечении 4-8 минут в случае неактивности, т.е. будут доступны, пока процесс приложения не завершит свою работу.
### LoadBalanceTimeout
When a connection is returned to the pool, its creation time is compared with the current time, and the connection is destroyed if that time span (in seconds) exceeds the value specified by Connection Lifetime. This is useful in clustered configurations to force load balancing between a running server and a server just brought online.
```C#
using System.Threading;
using Microsoft.Data.SqlClient;
using static System.Console;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)  
        {
            string connectionString = new SqlConnectionStringBuilder
            {
                ApplicationName = "ConsoleApp1"
                , DataSource = "localhost"
                , InitialCatalog = "test1"
                , IntegratedSecurity = true
                , LoadBalanceTimeout = 5
            }.ConnectionString;

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                Thread.Sleep(7000);
                WriteLine(con.ClientConnectionId);
            }
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                WriteLine(con.ClientConnectionId);
            }
        }
    }
}
```
#### Output:
```
d1cf5711-1fd8-41c2-ba05-66719782430d
cd3c320d-d94b-4270-a14a-41f838836109
```
Когда первый блок ```using (var con = new SqlConnection(connectionString))``` завершает свою работу, то созданное соединение помещается в пул, но с момента создания соединения прошло более 5 секунд благодаря ```Thread.Sleep(7000)```, поэтому данное подключение в пул не помещается. В результате работы программы создается два разных подключения. Второе соединение ```cd3c320d-d94b-4270-a14a-41f838836109``` будет помещено в пул и далее каждый раз при возвращении его в пул снова будет сравниваться время создания соединения с текущим временем, и если прошло более 5 секунд с момента создания, то данное соединение удаляется из пула (также удаляется, если соединение при нахождении в пуле было неактивным в течение 4-8 минут).
### MinPoolSize && LoadBalanceTimeout
В программе ниже только одно подключение останется в пуле после завершения первого блока ```using (var con = new SqlConnection(connectionString))```, т.к. срабатывает правило для ```LoadBalanceTimeout```. Но далее создается **новое** подключение с аналогичными параметрами, поскольку ```MinPoolSize=2```.
```C#
using System.Threading;
using Microsoft.Data.SqlClient;
using static System.Console;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)  
        {
            string connectionString = new SqlConnectionStringBuilder
            {
                ApplicationName = "ConsoleApp1"
                , DataSource = "localhost"
                , InitialCatalog = "test1"
                , IntegratedSecurity = true
                , LoadBalanceTimeout = 5
                , MinPoolSize = 2
            }.ConnectionString;

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                Thread.Sleep(7000);
                WriteLine(con.ClientConnectionId);
            }
            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                WriteLine(con.ClientConnectionId);
            }
        }
    }
}
```
### Links
[SQL Server Connection Pooling (ADO.NET)](https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-connection-pooling)\
[SqlConnection.ConnectionString Property](https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring?view=dotnet-plat-ext-5.0)

