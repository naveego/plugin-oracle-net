using System;
using System.Collections.Generic;

namespace PluginOracleNet.Helper
{
    public class Settings
    {
        public string Hostname { get; set; }
        public string Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ServiceName { get; set; }

        /// <summary>
        /// Validates the settings input object
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Validate()
        {
            if (String.IsNullOrEmpty(Hostname))
            {
                throw new Exception("The Hostname property must be set");
            }
            
            if (String.IsNullOrEmpty(ServiceName))
            {
                throw new Exception("The ServiceName property must be set");
            }

            if (String.IsNullOrEmpty(Username))
            {
                throw new Exception("The Username property must be set");
            }
            
            if (String.IsNullOrEmpty(Password))
            {
                throw new Exception("The Password property must be set");
            }

            if(String.IsNullOrEmpty(Port))
            {
                throw new Exception("The Port property must be set");
            }

        }

        /// <summary>
        /// Gets the database connection string
        /// </summary>
        /// <returns></returns>
        public string GetConnectionString()
        {
            return $"Data Source = (DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={Hostname})(PORT={Port}))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME={ServiceName}))); User Id = {Username}; Password = {Password};";
        }
    }
}