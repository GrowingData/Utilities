﻿using System;
using System.Linq;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;

using GrowingData.Utilities;
using GrowingData.Utilities.Csv;

namespace GrowingData.Utilities.Database {

	public class SqlServerBulkInserter : DbBulkInserter {
		//private SqlConnection _cn;

		protected Func<SqlConnection> _connectionFactory;

		public SqlServerBulkInserter(Func<DbConnection> targetConnection)
			: base(targetConnection) {

			_connectionFactory = () => {
				return targetConnection() as SqlConnection;
			};

		}


		public override DbTable GetDbSchema(string schema, string table) {
			var schemaReader = new SqlServerSchemaReader(_connectionFactory);
			return schemaReader.GetTables(schema, table).FirstOrDefault();
		}

		public override bool BulkInsert(string schemaName, string tableName, CsvReader reader) {



			using (var cn = _connectionFactory()) {
				using (SqlBulkCopy copy = new SqlBulkCopy(cn)) {

					var existingTable = GetDbSchema(schemaName, tableName);
					var readerTable = new DbTable(tableName, schemaName);

					for (var i = 0; i < reader.Columns.Count; i++) {
						var column = reader.Columns[i];
						readerTable.Columns.Add(column);
					}

					if (existingTable == null) {
						CreateTable(readerTable);
					}

					var table = GetDbSchema(schemaName, tableName);

					// Make sure the mappings are correct
					for (var i = 0; i < reader.Columns.Count; i++) {
						var column = reader.Columns[i].ColumnName;
						var sourceOrdinal = i;
						var destinationOrdinal = table.Columns.FindIndex(x => x.ColumnName == column);

						if (destinationOrdinal == -1) {
							var msg = string.Format("Unable to resolve column mapping, column: {0} was not found in destination table {1}",
								column,
								table.TableName
							);
							throw new Exception(msg);
						}
						copy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(i, destinationOrdinal));
					}

					copy.DestinationTableName = string.Format("[{0}].[{1}]", table.SchemaName, table.TableName);

					copy.BatchSize = 1000;
					copy.BulkCopyTimeout = 9999999;

					copy.WriteToServer(reader);
				}

			}

			return true;
		}


		public override bool BulkInsert(string schemaName, string tableName, DbDataReader reader, Action<long> eachRow) {
			using (var cn = _connectionFactory()) {
				using (SqlBulkCopy copy = new SqlBulkCopy(cn)) {

					var existingTable = GetDbSchema(schemaName, tableName);
					var readerTable = new DbTable(tableName, schemaName);

					for (var i = 0; i < reader.FieldCount; i++) {
						var columnName = reader.GetName(i);
						var columnType = reader.GetFieldType(i);

						var column = new DbColumn(columnName, MungType.Get(columnType));
						readerTable.Columns.Add(column);
					}

					if (existingTable == null) {
						CreateTable(readerTable);
					}

					var table = GetDbSchema(schemaName, tableName);

					for (var i = 0; i < reader.FieldCount; i++) {
						var column = reader.GetName(i);
						var sourceOrdinal = i;
						var destinationOrdinal = table.Columns.FindIndex(x => x.ColumnName == column);

						if (destinationOrdinal == -1) {
							var msg = string.Format("Unable to resolve column mapping, column: {0} was not found in destination table {1}",
								column,
								table.TableName
							);
							throw new Exception(msg);
						}
						copy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(i, destinationOrdinal));
					}

					copy.DestinationTableName = string.Format("[{0}].[{1}]", table.SchemaName, table.TableName);

					copy.BatchSize = 1000;
					copy.NotifyAfter = 1;
					copy.SqlRowsCopied += (sender, e) => {
						eachRow(e.RowsCopied);
					};
					copy.BulkCopyTimeout = 9999999;

					copy.WriteToServer(reader);
				}

			}
			return true;
		}

		private void Copy_SqlRowsCopied(object sender, SqlRowsCopiedEventArgs e) {
			throw new NotImplementedException();
		}

		public override bool CreateTable(DbTable table) {

			StringBuilder ddl = new System.Text.StringBuilder();
			ddl.Append(string.Format("CREATE TABLE [{0}].[{1}] (\r\n", table.SchemaName, table.TableName));


			foreach (var c in table.Columns) {
				var type = SqlServerTypeConverter.SqlServer.GetCreateColumnDefinition(c.MungType);
				ddl.Append(string.Format("\t[{0}] {1} NULL,\r\n", c.ColumnName, type));
			}
			ddl.Append(")");

			return ExecuteCommand(ddl.ToString());
		}

		public bool ExecuteCommand(string sql) {
			using (var cn = _connectionFactory()) {
				using (var cmd = new SqlCommand(sql, cn)) {
					cmd.ExecuteNonQuery();
				}
			}
			return true;

		}

		public bool SameColumn(DbColumn a, DbColumn b) {
			return a.ColumnName.ToLowerInvariant() == b.ColumnName.ToLowerInvariant();

		}


		public override bool ModifySchema(DbTable fromTbl, DbTable toTbl) {
			throw new NotImplementedException();
		}

	}


}