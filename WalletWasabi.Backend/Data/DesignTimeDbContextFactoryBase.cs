﻿using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace WalletWasabi.Backend.Data
{
	public abstract class DesignTimeDbContextFactoryBase<TContext> :
		IDesignTimeDbContextFactory<TContext> where TContext : DbContext
	{

		public TContext CreateDbContext(string[] args)
		{
			return Create(
				Directory.GetCurrentDirectory(),
				Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
		}

		protected abstract TContext CreateNewInstance(
			DbContextOptions<TContext> options);

		public TContext Create()
		{
			var environmentName =
				Environment.GetEnvironmentVariable(
					"ASPNETCORE_ENVIRONMENT");

			var basePath = AppContext.BaseDirectory;

			return Create(basePath, environmentName);
		}

		private TContext Create(string basePath, string environmentName)
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(basePath)
				.AddJsonFile("appsettings.json")
				.AddJsonFile($"appsettings.{environmentName}.json", true)
				.AddEnvironmentVariables();

			var config = builder.Build();

			var connstr = config.GetConnectionString("User ID=postgres;Host=127.0.0.1;Port=65466;Database=doesntmatterbecauseitisnotactuallyused;");
			return Create(connstr);
		}

		private TContext Create(string connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
			{
				throw new ArgumentException(
					$"{nameof(connectionString)} is null or empty.",
					nameof(connectionString));
			}

			var optionsBuilder =
				new DbContextOptionsBuilder<TContext>();

			Console.WriteLine(
				"MyDesignTimeDbContextFactory.Create(string): Connection string: {0}",
				connectionString);

			optionsBuilder.UseNpgsql(connectionString);

			DbContextOptions<TContext> options = optionsBuilder.Options;

			return CreateNewInstance(options);
		}
	}
}