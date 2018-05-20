using System;
using System.Collections.Generic;

using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence;

namespace AkkaPersistanceCs
{
	public class MyActor : ReceivePersistentActor
	{
		public class GetMessages { }

		private List<string> _msgs = new List<string>(); //INTERNAL STATE

		public override string PersistenceId => "1";

		public MyActor()
		{
			// recover
			Recover<string>(str => _msgs.Add(str));

			// commands
			Command<string>(str => Persist(str, s =>
			{
				_msgs.Add(str); //add msg to in-memory event store after persisting
			}));
			Command<GetMessages>(get => Sender.Tell(_msgs.AsReadOnly(), Self));
		}

	}

	class Program
	{
		static void Main(string[] args)
		{
			var config = ConfigurationFactory.ParseString(@"
akka {  
	stdout-loglevel = WARNING
	loglevel = DEBUG
	persistence.journal {
		plugin = ""akka.persistence.journal.sqlite""

		sqlite {
			class = ""Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite""
			plugin-dispatcher = ""akka.actor.default-dispatcher""
			connection-string = ""Data Source=JOURNAL.db;cache=shared;""
			connection-timeout = 30s

			schema-name = dbo

			table-name = event_journal

			auto-initialize = on

			timestamp-provider = ""Akka.Persistence.Sql.Common.Journal.DefaultTimestampProvider, Akka.Persistence.Sql.Common""
		}
		sql-server {
			class = ""Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer""
			connection-string = ""Data Source=localhost\\SQLEXPRESS;Initial Catalog=journal;Integrated Security=True;""
			schema-name = dbo

			auto-initialize = on
		}
	}
	actor {
		ask-timeout = 2000
		debug {
			# receive = on
			# autoreceive = on
			# lifecycle = on
			# event-stream = on
			unhandled = on
		}
	}
}");

			Console.WriteLine("Hello World!");

			using (var system = ActorSystem.Create("MyServer", config))
			{
				var actor = system.ActorOf<MyActor>("MyActor");

				actor.Tell("New session begin " + DateTime.Now);

				Console.WriteLine("Messages:");
				var messages = 
					actor.Ask<IReadOnlyList<string>>(new MyActor.GetMessages())
					.Result;
				foreach(var m in messages)
				{
					Console.WriteLine(m);
				}

				Console.WriteLine("Press enter to quit");
				Console.ReadLine();
			}

		}
	}
}
