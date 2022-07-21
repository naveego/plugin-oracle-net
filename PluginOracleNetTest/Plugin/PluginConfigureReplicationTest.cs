using System;
using System.Linq;
using System.Threading.Tasks;
using PluginOracleNet.API.Factory;
using PluginOracleNet.API.Replication;
using PluginOracleNet.DataContracts;
using PluginOracleNet.Helper;
using Xunit;

namespace PluginOracleNetTest.Plugin
{
    public class PluginConfigureReplicationTest
    {
        private static IConnectionFactory _connFactory;

        private async Task Setup()
        {
            _connFactory = new ConnectionFactory();
            IConnection conn = null;

            try
            {
                _connFactory.Initialize(new Settings
                {
                    Hostname = PluginIntegrationTest.SettingsHostname,
                    Port = PluginIntegrationTest.SettingsPort,
                    Password = PluginIntegrationTest.SettingsPassword,
                    Username = PluginIntegrationTest.SettingsUsername,
                    ServiceName = PluginIntegrationTest.SettingsServiceName
                });

                conn = _connFactory.GetConnection();
                await conn.OpenAsync();
            }
            finally
            {
                if (conn != null) await conn.CloseAsync();
            }
        }

        private void Cleanup()
        {
            _connFactory = null;
        }

        private ConfigureReplicationFormData GetReplicationForm(string schemaName = "")
        {
            return new ConfigureReplicationFormData()
            {
                SchemaName = string.IsNullOrWhiteSpace(schemaName) ? PluginIntegrationTest.SettingsUsername : schemaName,
                GoldenTableName = "gr_NaveegoValidationTestTable",
                VersionTableName = "vr_NaveegoValidationTestTable"
            };
        }

        [Fact]
        public async Task ConnectTest()
        {
            await Setup();
        }
        
        [Fact]
        public async Task NormalReplicationFormTest()
        {
            // setup
            await Setup();

            // act
            var errors =
                await Replication.TestReplicationFormData(GetReplicationForm("C##DEMO"), _connFactory);
            
            // assert
            Assert.Empty(errors);
            
            // cleanup
            Cleanup();
        }
        
        [Fact]
        public async Task SchemaNotExistsReplicationFormTest()
        {
            // setup
            await Setup();

            // act
            var errors =
                await Replication.TestReplicationFormData(GetReplicationForm("C##DEMO123"), _connFactory);

            // assert
            var error = errors.FirstOrDefault();
            Assert.Single(errors);
            Assert.NotNull(error);
            Assert.Equal("Schema \"C##DEMO123\" does not exist in the target database.", error);
            
            // cleanup
            Cleanup();
        }
        
        [Fact]
        public async Task SchemaOtherReplicationFormTest()
        {
            // setup
            await Setup();

            // act
            var errors =
                await Replication.TestReplicationFormData(GetReplicationForm("C##DEMO002"), _connFactory);

            // assert
            var error = errors.FirstOrDefault();
            Assert.Single(errors);
            Assert.NotNull(error);
            Assert.Equal("Unable to upsert into test table: ORA-01950: no privileges on tablespace 'USERS'.", error);
            
            // cleanup
            Cleanup();
        }
    }
}