using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Data.SqlClient;
using System.Data;

namespace ConsoleApp1
{
	class Program
	{
		static void Main(string[] args)
		{
			const string DOMAIN = "moscow.ru";
			const string GROUP_PREFIX = "MS_POWER_BI_";
			// Загружаем матрицу доступа
			DataSet dataset = new DataSet();
			using (SqlConnection connection = new SqlConnection("Data Source=;Initial Catalog=BIQube;Integrated Security=SSPI"))
			{
				connection.Open();
				using (SqlCommand command = new SqlCommand("select * from хд.МатрицаДоступа", connection))
				{
					SqlDataAdapter adapter = new SqlDataAdapter(command);
					adapter.Fill(dataset);
				}
			}
			DataTable accessMatrix = dataset.Tables[0]; // Матрица доступа
			var columns = accessMatrix.Columns; // Коды дивизонов

			Dictionary<string, List<string>> divisions = new Dictionary<string, List<string>>();

			DataTable destination = new DataTable();
			//dt.DefaultView.ToTable(true);
			destination.Columns.Add(new DataColumn("КодПредприятия", typeof(string)));
			destination.Columns.Add(new DataColumn("Пользователь", typeof(string)));

			using (PrincipalContext context = new PrincipalContext(ContextType.Domain, DOMAIN))
			{
				// Получаем список пользователей, входящих в группы дивизионов
				for (int k = 1; k < columns.Count; ++k)
				{
					divisions.Add(columns[k].ColumnName, new List<string>(30));
					using (GroupPrincipal group = GroupPrincipal.FindByIdentity(context, GROUP_PREFIX + columns[k].ColumnName))
					{
						if (group != null)
						{
							foreach (var member in group.Members)
							{
								using (UserPrincipal user = member as UserPrincipal)
								{
									if (user != null)
									{
										string userDomain = user.Context.Name.Substring(0, user.Context.Name.IndexOf("."));
										divisions[columns[k].ColumnName].Add(string.Concat(userDomain, @"\", user.SamAccountName));
									}
								}
							}
						}
					}
				}
				// Добавляем список пользователей, входящих в группы предприятий
				// В accessMatrix.Rows[i].Field(0) хранится код предприятия
				for (int i = 0; i < accessMatrix.Rows.Count; ++i)
				{
					string orgCode = accessMatrix.Rows[i].Field<string>(0);
					//using (GroupPrincipal group = GroupPrincipal.FindByIdentity(context, "MS_POWER_BI_XCMIN")) // для теста
					using (GroupPrincipal group = GroupPrincipal.FindByIdentity(context, GROUP_PREFIX + orgCode))
					{
						if (group != null)
						{
							foreach (var member in group.Members)
							{
								using (UserPrincipal user = member as UserPrincipal)
								{
									if (user != null)
									{
										string domain = user.Context.Name.Substring(0, user.Context.Name.IndexOf("."));
										destination.Rows.Add(new string[] { orgCode, string.Concat(domain, @"\", user.SamAccountName) });
									}
								}
							}
						}
					}
					// Добавляем пользователей, входящих в группы дивизионов
					for (int k = 1; k < columns.Count; ++k)
					{
						// Проверяем, имеется ли доступ к предприятию у пользователей, входящих в группу дивизиона
						if (accessMatrix.Rows[i].Field<bool>(k))
						{
							foreach (var userLogin in divisions[columns[k].ColumnName])
								destination.Rows.Add(new string[] { orgCode, userLogin });
						}
					}
				}
			}
			using (SqlConnection connection = new SqlConnection("Data Source;Initial Catalog=BIQube_Staging;Integrated Security=SSPI"))
			{
				connection.Open();
				using (SqlTransaction transaction = connection.BeginTransaction())
				{
					using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, transaction))
					{
						bulkCopy.DestinationTableName = "prj.ПраваДоступа";
						bulkCopy.ColumnMappings.Add("КодПредприятия", "КодПредприятия");
						bulkCopy.ColumnMappings.Add("Пользователь", "Пользователь");
						try
						{
							// Заполняем таблицу уникальными парами значений (Пользователь, КодПредприятия)
							bulkCopy.WriteToServer(destination.DefaultView.ToTable(true));
							transaction.Commit();
						}
						catch (System.Exception)
						{
							transaction.Rollback();
							throw;
						}
					}
				}
			}
		}
	}
}
