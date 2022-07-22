using System;
using System.Collections.Generic;
using PluginOracleNet.Helper;
using PluginOracleNet.DataContracts;
using Newtonsoft.Json;
using Naveego.Sdk.Plugins;
using Xunit;
using System.Threading.Tasks;
using Grpc.Core;
using System.Linq;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Replication;
using Record = Naveego.Sdk.Plugins.Record;

namespace PluginOracleNetTest.Plugin
{
    public class PluginIntegrationTest
    {
        // Test Variables

        private static string TestSchemaID = "\"<schema_name>\".\"<table_name>\"";
        private static string TestSchemaName = "<schema_name>.<table_name>";
        private static int TestSampleCount = 10;
        private static int TestPropertyCount = 11;
        
        private static string TestSchemaID_2 = "\"<schema_name>\".\"<table_name>02\"";
        private static string TestSchemaName_2 = "<schema_name>.<table_name>02";
        private static int TestSampleCount_2 = 10;
        private static int TestPropertyCount_2 = 11;

        private static string TestPropertyID = "\"ID\"";
        private static string TestPropertyName = "ID";

        internal static string SettingsHostname = "";
        internal static string SettingsPassword = "";
        internal static string SettingsPort = "";
        internal static string SettingsServiceName = "";
        internal static string SettingsUsername = "";

        private Settings GetSettings()
        {
            return new Settings
            {
                Hostname = SettingsHostname,
                Port = SettingsPort,
                Password = SettingsPassword,
                Username = SettingsUsername,
                ServiceName = SettingsServiceName
            };
        }

        private ConnectRequest GetConnectSettings()
        {
            var settings = GetSettings();

            return new ConnectRequest
            {
                SettingsJson = JsonConvert.SerializeObject(settings),
                OauthConfiguration = new OAuthConfiguration(),
                OauthStateJson = ""
            };
        }

        private Schema GetTestSchema(string id = "test", string name = "test", string query = "")
        {
            return new Schema
            {
                Id = id,
                Name = name,
                Query = query,
                Properties =
                {
                    new Property
                    {
                        Id = "Id",
                        Name = "Id",
                        Type = PropertyType.Integer,
                        IsKey = true
                    },
                    new Property
                    {
                        Id = "Name",
                        Name = "Name",
                        Type = PropertyType.String
                    }
                }
            };
        }

        private Schema GetTestReplicationSchema(string id = "test", string name = "test", string query = "")
        {
            // --- Note: Changed to fit the schema of the ACCOUNTARCHIVE_GHTesting table ---
            // Change query if empty
            if (string.IsNullOrWhiteSpace(query))
            {
                query = TestSchemaID;
            }
            
            // build schema
            return new Schema
            {
                Id = id,
                Name = name,
                Query = query,
                Properties =
                {
                    new Property
                    {
                        Id = "ID",
                        Name = "ID",
                        Type = PropertyType.String,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "FIRST_NAME",
                        Name = "FIRST_NAME",
                        Type = PropertyType.String,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "LAST_NAME",
                        Name = "LAST_NAME",
                        Type = PropertyType.String,
                        IsKey = false
                    }
                    ,
                    new Property
                    {
                        Id = "EMAIL",
                        Name = "EMAIL",
                        Type = PropertyType.String,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "ADDRESS",
                        Name = "ADDRESS",
                        Type = PropertyType.String,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "CITY",
                        Name = "CITY",
                        Type = PropertyType.String,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "STATE",
                        Name = "STATE",
                        Type = PropertyType.String,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "ZIP",
                        Name = "ZIP",
                        Type = PropertyType.Integer,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "GENERAL_LEDGER",
                        Name = "GENERAL_LEDGER",
                        Type = PropertyType.Integer,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "BALANCE",
                        Name = "BALANCE",
                        Type = PropertyType.Decimal,
                        IsKey = false
                    },
                    new Property
                    {
                        Id = "REP",
                        Name = "REP",
                        Type = PropertyType.String,
                        IsKey = false
                    }
                }
            };
        }
        
        [Fact]
        public async Task ConnectSessionTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var request = GetConnectSettings();
            var disconnectRequest = new DisconnectRequest();

            // act
            var response = client.ConnectSession(request);
            var responseStream = response.ResponseStream;
            var records = new List<ConnectResponse>();

            while (await responseStream.MoveNext())
            {
                records.Add(responseStream.Current);
                client.Disconnect(disconnectRequest);
            }

            // assert
            Assert.Single(records);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task ConnectTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var request = GetConnectSettings();

            // act
            var response = client.Connect(request);

            // assert
            Assert.IsType<ConnectResponse>(response);
            Assert.Equal("", response.SettingsError);
            Assert.Equal("", response.ConnectionError);
            Assert.Equal("", response.OauthError);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }
        
        [Fact]
        public async Task ConnectFailedTest()
        {
            // setup
            Server server = new Server
            {
                Services = {Publisher.BindService(new PluginOracleNet.Plugin.Plugin())},
                Ports = {new ServerPort("localhost", 0, ServerCredentials.Insecure)}
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            const string wrongUsername = "ITSALLWRONG";

            var request = new ConnectRequest
            {
                SettingsJson = JsonConvert.SerializeObject(new Settings
                {
                    // When testing, enter same info from GetSettings() w/ wrongUsername as Username
                    Hostname = SettingsHostname,
                    Port = SettingsPort,
                    Password = SettingsPassword,
                    Username = wrongUsername,
                    ServiceName = SettingsServiceName
                }),
                OauthConfiguration = new OAuthConfiguration(),
                OauthStateJson = ""
            };

            // act
            var response = client.Connect(request);

            // assert
            Assert.IsType<ConnectResponse>(response);
            Assert.Equal("", response.SettingsError);
            Assert.Equal($"ORA-01017: invalid username/password; logon denied", response.ConnectionError);
            Assert.Equal("", response.OauthError);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        // CLOB
        [Fact]
        public async Task DiscoverSchemasAllTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var request = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.All,
                SampleSize = 10
            };

            // act
            client.Connect(connectRequest);
            var response = client.DiscoverSchemas(request);

            // assert
            Assert.IsType<DiscoverSchemasResponse>(response);
            Assert.Equal(39, response.Schemas.Count);

            // --- Detect First Column in testing table ---
            var schema = response.Schemas[1]; // Use testing table

            Assert.Equal(TestSchemaID, schema.Id);
            Assert.Equal(TestSchemaName, schema.Name);
            Assert.Equal("", schema.Query);
            Assert.Equal(TestSampleCount, schema.Sample.Count);
            Assert.Equal(TestPropertyCount, schema.Properties.Count);

            var property = schema.Properties[0];
            Assert.Equal(TestPropertyID, property.Id);
            Assert.Equal(TestPropertyName, property.Name);
            Assert.Equal("", property.Description);
            Assert.Equal(PropertyType.String, property.Type);
            Assert.False(property.IsKey);
            Assert.True(property.IsNullable);
            
            // --- Detect Primary Key in last table ---
            // Use the test schema
            var schema2 = response.Schemas.Single(s => s.Id == TestSchemaID_2);

            Assert.Equal(TestSchemaID_2, schema2.Id);
            Assert.Equal(TestSchemaName_2, schema2.Name);
            Assert.Equal("", schema2.Query);
            Assert.Equal(TestSampleCount_2, schema2.Sample.Count);
            Assert.Equal(TestPropertyCount_2, schema2.Properties.Count);

            var property2 = schema2.Properties[0];
            Assert.Equal(TestPropertyID, property2.Id);
            Assert.Equal(TestPropertyName, property2.Name);
            Assert.Equal("", property2.Description);
            Assert.Equal(PropertyType.String, property2.Type);
            Assert.True(property2.IsKey);
            Assert.False(property2.IsNullable);
            

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task DiscoverSchemasRefreshTableTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            int port = server.Ports.First().BoundPort;

            Channel channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            Publisher.PublisherClient client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var request = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.Refresh,
                SampleSize = 10,
                ToRefresh = { GetTestSchema(TestSchemaID, TestSchemaName) }
            };

            // act
            client.Connect(connectRequest);
            var response = client.DiscoverSchemas(request);

            // assert
            Assert.IsType<DiscoverSchemasResponse>(response);
            Assert.Single(response.Schemas);

            var schema = response.Schemas[0];
            Assert.Equal(TestSchemaID, schema.Id);
            Assert.Equal(TestSchemaName, schema.Name);
            Assert.Equal("", schema.Query);
            Assert.Equal(TestSampleCount, schema.Sample.Count);
            Assert.Equal(TestPropertyCount, schema.Properties.Count);

            var property = schema.Properties[0];
            Assert.Equal(TestPropertyID, property.Id);
            Assert.Equal(TestPropertyName, property.Name);
            Assert.Equal("", property.Description);
            Assert.Equal(PropertyType.String, property.Type);
            Assert.False(property.IsKey);
            Assert.True(property.IsNullable);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task DiscoverSchemasRefreshQueryTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var request = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.Refresh,
                SampleSize = 10,
                ToRefresh = { GetTestSchema("test", "test", $"SELECT * FROM {TestSchemaID}") }
            };

            // act
            client.Connect(connectRequest);
            var response = client.DiscoverSchemas(request);

            // assert
            Assert.IsType<DiscoverSchemasResponse>(response);
            Assert.Single(response.Schemas);

            var schema = response.Schemas[0];
            Assert.Equal($"test", schema.Id);
            Assert.Equal("test", schema.Name);
            Assert.Equal($"SELECT * FROM {TestSchemaID}", schema.Query);
            Assert.Equal(TestSampleCount, schema.Sample.Count);
            Assert.Equal(TestPropertyCount, schema.Properties.Count);

            var property = schema.Properties[0];
            Assert.Equal(TestPropertyID, property.Id);
            Assert.Equal(TestPropertyName, property.Name);
            Assert.Equal("", property.Description);
            Assert.Equal(PropertyType.String, property.Type);
            Assert.False(property.IsKey);
            Assert.True(property.IsNullable);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task DiscoverSchemasRefreshQueryBadSyntaxTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var request = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.Refresh,
                SampleSize = 10,
                ToRefresh = { GetTestSchema("bad syntax") }
            };

            // act
            client.Connect(connectRequest);

            try
            {
                var response = client.DiscoverSchemas(request);
            }
            catch (Exception e)
            {
                // assert
                Assert.IsType<RpcException>(e);
                Assert.Contains("ORA-", e.Message);
            }

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task ReadStreamTableSchemaTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var schema = GetTestSchema(TestSchemaID, TestSchemaName);

            var connectRequest = GetConnectSettings();

            var schemaRequest = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.Refresh,
                ToRefresh = { schema }
            };

            var request = new ReadRequest()
            {
                DataVersions = new DataVersions
                {
                    JobId = "test"
                },
                JobId = "test",
            };

            // act
            client.Connect(connectRequest);
            var schemasResponse = client.DiscoverSchemas(schemaRequest);
            request.Schema = schemasResponse.Schemas[0];

            var response = client.ReadStream(request);
            var responseStream = response.ResponseStream;
            var records = new List<Naveego.Sdk.Plugins.Record>();

            while (await responseStream.MoveNext())
            {
                records.Add(responseStream.Current);
            }

            // assert
            Assert.Equal(424, records.Count);

            var record = JsonConvert.DeserializeObject<Dictionary<string, object>>(records[0].DataJson);
            Assert.Equal("dc6fdfef-812c-4c98-93cd-a4f839416c99", record["\"ID\""]);
            Assert.Equal("Arlette", record["\"FIRST_NAME\""]);
            Assert.Equal("Stopher", record["\"LAST_NAME\""]);
            Assert.Equal("Spokane", record["\"CITY\""]);
            Assert.Equal("WA", record["\"STATE\""]);
            Assert.Equal("8 Golden Leaf Drive", record["\"ADDRESS\""]);
            Assert.Equal("99205", record["\"ZIP\""]);
            Assert.Equal("Erica Smith", record["\"REP\""]);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task ReadStreamQuerySchemaTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var schema = GetTestSchema("test", "test", $"SELECT * FROM {TestSchemaID}");
            
            var connectRequest = GetConnectSettings();

            var schemaRequest = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.Refresh,
                ToRefresh = { schema }
            };

            var request = new ReadRequest()
            {
                DataVersions = new DataVersions
                {
                    JobId = "test"
                },
                JobId = "test",
            };

            // act
            client.Connect(connectRequest);
            var schemasResponse = client.DiscoverSchemas(schemaRequest);
            request.Schema = schemasResponse.Schemas[0];

            var response = client.ReadStream(request);
            var responseStream = response.ResponseStream;
            var records = new List<Naveego.Sdk.Plugins.Record>();

            while (await responseStream.MoveNext())
            {
                records.Add(responseStream.Current);
            }

            // assert
            Assert.Equal(424, records.Count);

            var record = JsonConvert.DeserializeObject<Dictionary<string, object>>(records[0].DataJson);
            Assert.Equal("dc6fdfef-812c-4c98-93cd-a4f839416c99", record["\"ID\""]);
            Assert.Equal("Arlette", record["\"FIRST_NAME\""]);
            Assert.Equal("Stopher", record["\"LAST_NAME\""]);
            Assert.Equal("Spokane", record["\"CITY\""]);
            Assert.Equal("WA", record["\"STATE\""]);
            Assert.Equal("8 Golden Leaf Drive", record["\"ADDRESS\""]);
            Assert.Equal("99205", record["\"ZIP\""]);
            Assert.Equal("Erica Smith", record["\"REP\""]);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

                [Fact]
        public async Task ReadStreamLimitTest()
        {
            // setup
            Server server = new Server
            {
                Services = { Publisher.BindService(new PluginOracleNet.Plugin.Plugin()) },
                Ports = { new ServerPort("localhost", 0, ServerCredentials.Insecure) }
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var schema = GetTestSchema("test", "test", $"SELECT * FROM {TestSchemaID}");

            var connectRequest = GetConnectSettings();

            var schemaRequest = new DiscoverSchemasRequest
            {
                Mode = DiscoverSchemasRequest.Types.Mode.Refresh,
                ToRefresh = { schema }
            };

            var request = new ReadRequest()
            {
                DataVersions = new DataVersions
                {
                    JobId = "test"
                },
                JobId = "test",
                Limit = 10
            };

            // act
            client.Connect(connectRequest);
            var schemasResponse = client.DiscoverSchemas(schemaRequest);
            request.Schema = schemasResponse.Schemas[0];

            var response = client.ReadStream(request);
            var responseStream = response.ResponseStream;
            var records = new List<Naveego.Sdk.Plugins.Record>();

            while (await responseStream.MoveNext())
            {
                records.Add(responseStream.Current);
            }

            // assert
            Assert.Equal(10, records.Count);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }
        
        [Fact]
        public async Task PrepareWriteTest()
        {
            // setup
            Server server = new Server
            {
                Services = {Publisher.BindService(new PluginOracleNet.Plugin.Plugin())},
                Ports = {new ServerPort("localhost", 0, ServerCredentials.Insecure)}
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var request = new PrepareWriteRequest()
            {
                Schema = GetTestSchema(),
                CommitSlaSeconds = 1,
                Replication = new ReplicationWriteRequest
                {
                    SettingsJson = JsonConvert.SerializeObject(new ConfigureReplicationFormData
                    {
                        SchemaName = "C##Demo",
                        GoldenTableName = "gr_test",
                        VersionTableName = "vr_test"
                    })
                },
                DataVersions = new DataVersions
                {
                    JobId = "jobUnitTest",
                    ShapeId = "shapeUnitTest",
                    JobDataVersion = 1,
                    ShapeDataVersion = 2
                }
            };

            // act
            client.Connect(connectRequest);
            var response = client.PrepareWrite(request);

            // assert
            Assert.IsType<PrepareWriteResponse>(response);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }

        [Fact]
        public async Task WriteTest()
        {
            // setup
            Server server = new Server
            {
                Services = {Publisher.BindService(new PluginOracleNet.Plugin.Plugin())},
                Ports = {new ServerPort("localhost", 0, ServerCredentials.Insecure)}
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var configureRequest = new ConfigureWriteRequest
            {
                Form = new ConfigurationFormRequest
                {
                    DataJson = JsonConvert.SerializeObject(new ConfigureWriteFormData
                    {
                        StoredProcedure = "UpsertIntoAccountArchive_GHTesting"
                    })
                }
            };

            var records = new List<Record>()
            {
                new Record
                {
                    Action = Record.Types.Action.Upsert,
                    CorrelationId = "ACCOUNTARCHIVE_GHTesting",
                    RecordId = "record1",
                    DataJson = @"{
    ""U_ID"":""aaaaaaaa-2222-4e8e-99b4-7f8bb172bf9a"",
    ""U_FIRST_NAME"":""Test"",
    ""U_LAST_NAME"":""First"",
    ""U_EMAIL"":""test.first@email.net"",
    ""U_ADDRESS"":""1234 Test Road"",
    ""U_CITY"":""Test"",
    ""U_STATE"":""MI"",
    ""U_ZIP"":""55555"",
    ""U_GENERAL_LEDGER"":""11112"",
    ""U_BALANCE"":""0.00"",
    ""U_REP"":""Ron Jordans""
}",
                }
            };

            var recordAcks = new List<RecordAck>();

            // act
            client.Connect(connectRequest);

            var configureResponse = client.ConfigureWrite(configureRequest);

            var prepareWriteRequest = new PrepareWriteRequest()
            {
                Schema = configureResponse.Schema,
                CommitSlaSeconds = 1000,
                DataVersions = new DataVersions
                {
                    JobId = "jobUnitTest",
                    ShapeId = "shapeUnitTest",
                    JobDataVersion = 1,
                    ShapeDataVersion = 1
                }
            };
            client.PrepareWrite(prepareWriteRequest);

            using (var call = client.WriteStream())
            {
                var responseReaderTask = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext())
                    {
                        var ack = call.ResponseStream.Current;
                        recordAcks.Add(ack);
                    }
                });

                foreach (Record record in records)
                {
                    await call.RequestStream.WriteAsync(record);
                }

                await call.RequestStream.CompleteAsync();
                await responseReaderTask;
            }

            // assert
            Assert.Single(recordAcks);
            Assert.Equal("", recordAcks[0].Error);
            Assert.Equal("ACCOUNTARCHIVE_GHTesting", recordAcks[0].CorrelationId);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }
        
        [Fact]
        public async Task ReplicationWriteTest()
        {
            // setup
            Server server = new Server
            {
                Services = {Publisher.BindService(new PluginOracleNet.Plugin.Plugin())},
                Ports = {new ServerPort("localhost", 0, ServerCredentials.Insecure)}
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();

            var prepareWriteRequest = new PrepareWriteRequest()
            {
                Schema = GetTestReplicationSchema(),
                CommitSlaSeconds = 1000,
                Replication = new ReplicationWriteRequest
                {
                    SettingsJson = JsonConvert.SerializeObject(new ConfigureReplicationFormData
                    {
                        SchemaName = "C##Demo",
                        GoldenTableName = "gr_test",
                        VersionTableName = "vr_test"
                    })
                },
                DataVersions = new DataVersions
                {
                    JobId = "jobUnitTest",
                    ShapeId = "shapeUnitTest",
                    JobDataVersion = 1,
                    ShapeDataVersion = 1
                }
            };

            var records = new List<Record>()
            {
                new Record
                {
                    Action = Record.Types.Action.Upsert,
                    CorrelationId = "test",
                    RecordId = "record1",
                    //DataJson = $"{{\"Id\":1,\"Name\":\"Test Company\",\"Date\":\"{DateTime.Now.Date}\",\"Time\":\"{DateTime.Now:hh:mm:ss}\",\"Decimal\":\"13.04\"}}",
                    DataJson = @"{
    ""ID"":""aaaaaaaa-1313-4e8e-99b4-7f8bb172bf9a"",
    ""FIRST_NAME"":""Test"",
    ""LAST_NAME"":""Second"",
    ""EMAIL"":""test.second@email.net"",
    ""ADDRESS"":""5678 Test Road"",
    ""CITY"":""Test"",
    ""STATE"":""MI"",
    ""ZIP"":""91952"",
    ""GENERAL_LEDGER"":""11190"",
    ""BALANCE"":""1.00"",
    ""REP"":""Ron Jordans""
}".Replace("\n", ""),
                    Versions =
                    {
                        new RecordVersion
                        {
                            RecordId = "version1",
                            //DataJson = $"{{\"Id\":1,\"Name\":\"Test Company\",\"Date\":\"{DateTime.Now.Date}\",\"Time\":\"{DateTime.Now:hh:mm:ss}\",\"Decimal\":\"13.04\"}}"
                            DataJson = @"{
    ""ID"":""aaaaaaaa-1313-4e8e-99b4-7f8bb172bf9a"",
    ""FIRST_NAME"":""Test"",
    ""LAST_NAME"":""Second"",
    ""EMAIL"":""test.second@email.net"",
    ""ADDRESS"":""5678 Test Road"",
    ""CITY"":""Test"",
    ""STATE"":""MI"",
    ""ZIP"":""91952"",
    ""GENERAL_LEDGER"":""11190"",
    ""BALANCE"":""1.00"",
    ""REP"":""Ron Jordans""
}".Replace("\n", "")
                        }
                    }
                }
            };

            var recordAcks = new List<RecordAck>();

            // act
            client.Connect(connectRequest);
            client.PrepareWrite(prepareWriteRequest);

            using (var call = client.WriteStream())
            {
                var responseReaderTask = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext())
                    {
                        var ack = call.ResponseStream.Current;
                        recordAcks.Add(ack);
                    }
                });

                foreach (Record record in records)
                {
                    await call.RequestStream.WriteAsync(record);
                }

                await call.RequestStream.CompleteAsync();
                await responseReaderTask;
            }

            // assert
            Assert.Single(recordAcks);
            Assert.Equal("", recordAcks[0].Error);
            Assert.Equal("test", recordAcks[0].CorrelationId);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }
        
        [Fact]
        public async Task ConfigureReplicationTest()
        {
            // setup
            Server server = new Server
            {
                Services = {Publisher.BindService(new PluginOracleNet.Plugin.Plugin())},
                Ports = {new ServerPort("localhost", 0, ServerCredentials.Insecure)}
            };
            server.Start();

            var port = server.Ports.First().BoundPort;

            var channel = new Channel($"localhost:{port}", ChannelCredentials.Insecure);
            var client = new Publisher.PublisherClient(channel);

            var connectRequest = GetConnectSettings();
            
            var request = new ConfigureReplicationRequest
            {
                Form = new ConfigurationFormRequest
                {
                    DataJson = JsonConvert.SerializeObject(
                        new ConfigureReplicationFormData
                        {
                            SchemaName = "C##DEMO",
                            GoldenTableName = "gr_test",
                            VersionTableName = "vr_test"
                        }
                    )
                },
                Schema = GetTestReplicationSchema()
            };

            // act
            client.Connect(connectRequest);
            var response = client.ConfigureReplication(request);

            // assert
            Assert.IsType<ConfigureReplicationResponse>(response);
            Assert.Empty(response.Form.Errors);

            // cleanup
            await channel.ShutdownAsync();
            await server.ShutdownAsync();
        }
    }
}
