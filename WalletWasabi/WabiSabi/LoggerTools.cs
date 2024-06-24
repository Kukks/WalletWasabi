using NBitcoin;
using System.Runtime.CompilerServices;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi;

public static class LoggerTools
{
	public static void Log(this Round round, IWallet wallet, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		var roundState = RoundState.FromRound(round);
		roundState.Log(wallet,logLevel, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this Round round,IWallet wallet,  string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round,wallet, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogWarning(this Round round,IWallet wallet,  string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round,wallet, LogLevel.Warning, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogError(this Round round,IWallet wallet,  string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, wallet,LogLevel.Error, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void Log(this RoundState roundState, IWallet wallet,  LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		string round = !roundState.IsBlame ? "Round" : "Blame Round";
		round += $" ({roundState.Id.ToString()[..7] + "..." + roundState.Id.ToString()[^7..]})";
		if (wallet is null)
		{

			Logger.Log(logLevel, $"{round}: {logMessage}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
		}
		else
		{
			wallet.Log(logLevel, $"{round}: {logMessage}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
		}
	}

	public static void LogInfo(this RoundState roundState, IWallet wallet,  string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(roundState, wallet,LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogDebug(this RoundState roundState, IWallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(roundState,wallet, LogLevel.Debug, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void Log(this IWallet wallet, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		wallet.Log(logLevel, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this IWallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogDebug(this IWallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Debug, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogWarning(this IWallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Warning, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogError(this IWallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Error, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogTrace(this IWallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Trace, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}
}
