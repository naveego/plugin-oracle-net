using System;
using System.Data;
using Aunalytics.Sdk.Plugins;

namespace PluginOracleNet.API.Discover
{
    public static partial class Discover
    {
        private static PropertyType GetPropertyType(DataRow row)
        {
            var type = Type.GetType(row["DataType"].ToString());
            switch (true)
            {
                case bool _ when type == typeof(bool):
                    return PropertyType.Bool;
                case bool _ when type == typeof(int):
                case bool _ when type == typeof(long):
                    return PropertyType.Integer;
                case bool _ when type == typeof(float):
                case bool _ when type == typeof(double):
                    return PropertyType.Float;
                case bool _ when type == typeof(DateTime):
                    return PropertyType.Datetime;
                case bool _ when type == typeof(Decimal):
                    return PropertyType.Decimal;
                case bool _ when type == typeof(string):
                    if (Int64.Parse(row["ColumnSize"].ToString()) > 500)
                    {
                        return PropertyType.Text;
                    }

                    return PropertyType.String;
                default:
                    return PropertyType.String;
            }
        }
    }
}