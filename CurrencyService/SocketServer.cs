using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CurrencyService
{
    public class SocketServer
    {
        private TcpListener listener;
        private List<TcpClient> clients = new List<TcpClient>();
        private bool isRunning = false;

        public void Start()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, 5050);
                listener.Start();
                isRunning = true;
                Console.WriteLine("Socket server started on port 5050...");
                Thread listenerThread = new Thread(ListenLoop) { IsBackground = true };
                listenerThread.Start();
                StartBroadcasting();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Socket server failed to start: " + ex.Message);
            }
        }

        private void ListenLoop()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    clients.Add(client);
                    Console.WriteLine("Client connected");
                    Thread clientThread = new Thread(() => HandleClient(client)) { IsBackground = true };
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error accepting client: " + ex.Message);
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];

                string welcome = "Connected to Currency Exchange Socket Server.\n" +
                                 "You can use commands:\n" +
                                 "- CREATEACCOUNT <Username> <Password>\n" +
                                 "- LOGIN <Username> <Password>\n" +
                                 "- RATE <CurrencyCode>\n" +
                                 "- BALANCE <Username>\n" +
                                 "- BUY <CurrencyCode> <Amount> <Username>\n" +
                                 "- SELL <CurrencyCode> <Amount> <Username>\n" +
                                 "- PAY <FromUser> <ToUser> <Amount>\n" +
                                 "- MYCURRENCIES <Username>\n" +
                                 "- HISTORY <Username>\n" +
                                 "- HELP\n";
                byte[] welcomeBytes = Encoding.UTF8.GetBytes(welcome);
                stream.Write(welcomeBytes, 0, welcomeBytes.Length);

                while (client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        Console.WriteLine($"📨 Received from client: {message}");
                        string response = HandleCommand(message);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
                        stream.Write(responseBytes, 0, responseBytes.Length);
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in client handler: " + ex.Message);
            }
            finally
            {
                client.Close();
                Console.WriteLine("Client disconnected");
            }
        }

        private string HandleCommand(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "Empty command.";
            string[] parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "Invalid command format.";

            string cmd = parts[0].ToUpperInvariant();
            var service = new CurrencyService();

            try
            {
                switch (cmd)
                {
                    case "HELP":
                        return "Available commands:\n" +
                               "- CREATEACCOUNT <Username> <Password>\n" +
                               "- LOGIN <Username> <Password>\n" +
                               "- RATE <CurrencyCode>\n" +
                               "- BALANCE <Username>\n" +
                               "- BUY <CurrencyCode> <Amount> <Username>\n" +
                               "- SELL <CurrencyCode> <Amount> <Username>\n" +
                               "- PAY <FromUser> <ToUser> <Amount>\n" +
                               "- MYCURRENCIES <Username>\n" +
                               "- HISTORY <Username>";

                    case "CREATEACCOUNT" when parts.Length == 3:
                        return service.CreateAccount(parts[1], parts[2])
                            ? $"✅ Account created for {parts[1]}"
                            : $"❌ Account already exists for {parts[1]}";

                    case "LOGIN" when parts.Length == 3:
                        return service.Login(parts[1], parts[2])
                            ? $"✅ Login successful for {parts[1]}"
                            : $"❌ Login failed for {parts[1]}";

                    case "RATE" when parts.Length == 2:
                        var rate = service.GetExchangeRate(parts[1]);
                        return rate > 0
                            ? $"💱 1 {parts[1]} = {rate} PLN"
                            : $"❌ Rate for '{parts[1]}' not found.";

                    case "BALANCE" when parts.Length == 2:
                        var balance = service.GetBalance(parts[1]);
                        return balance >= 0
                            ? $"💰 Balance for {parts[1]}: {balance}"
                            : $"❌ User '{parts[1]}' not found.";

                    case "BUY":
                        if (parts.Length != 4)
                            return "❌ Invalid BUY format. Use: BUY <CurrencyCode> <Amount> <Username>";
                        if (!double.TryParse(parts[2], out double bAmount))
                            return "❌ Invalid amount in BUY command.";
                        return service.BuyCurrency(parts[3], parts[1], bAmount)
                            ? $"✅ Bought {bAmount} {parts[1]} for {parts[3]}"
                            : $"❌ Buy failed for {parts[3]}";

                    case "SELL":
                        if (parts.Length != 4)
                            return "❌ Invalid SELL format. Use: SELL <CurrencyCode> <Amount> <Username>";
                        if (!double.TryParse(parts[2], out double sAmount))
                            return "❌ Invalid amount in SELL command.";
                        return service.SellCurrency(parts[3], parts[1], sAmount)
                            ? $"✅ Sold {sAmount} {parts[1]} for {parts[3]}"
                            : $"❌ Sell failed for {parts[3]}";

                    case "PAY" when parts.Length == 4 && double.TryParse(parts[3], out double payAmount):
                        return service.VirtualPayment(parts[1], parts[2], payAmount)
                            ? $"✅ Payment of {payAmount} from {parts[1]} to {parts[2]} successful."
                            : $"❌ Payment failed. Check balance or usernames.";

                    case "MYCURRENCIES" when parts.Length == 2:
                        var holdings = service.GetUserCurrencies(parts[1]);
                        if (holdings.Count == 0) return $"📦 No currencies held by {parts[1]}.";
                        var holdingsText = new StringBuilder($"💼 Holdings for {parts[1]}:\n");
                        foreach (var kv in holdings)
                            holdingsText.AppendLine($"{kv.Key}: {kv.Value}");
                        return holdingsText.ToString();

                    case "HISTORY" when parts.Length == 2:
                        var history = service.GetTransactionHistory(parts[1]);
                        if (history.Count == 0) return $"📬 No transactions for {parts[1]}.";
                        var historySb = new StringBuilder($"📜 History for {parts[1]}:\n");
                        foreach (var tx in history)
                            historySb.AppendLine($"{tx.Date:yyyy-MM-dd HH:mm:ss} | {tx.Type} {tx.Amount} {tx.CurrencyCode} @ {tx.Rate}");
                        return historySb.ToString();

                    default:
                        return "❓ Unknown command. Type HELP for options.";
                }
            }
            catch (Exception ex)
            {
                return $"❌ Error processing command: {ex.Message}";
            }
        }

        private void StartBroadcasting()
        {
            new Thread(() =>
            {
                while (isRunning)
                {
                    try
                    {
                        var service = new CurrencyService();
                        string[] currencies = { "USD", "EUR", "GBP" };

                        foreach (var code in currencies)
                        {
                            try
                            {
                                double rate = service.GetExchangeRate(code);
                                if (rate > 0)
                                {
                                    string msg = $"🔄 Live update: {code} = {rate} PLN\n";
                                    byte[] data = Encoding.UTF8.GetBytes(msg);
                                    foreach (var client in clients)
                                    {
                                        if (client.Connected)
                                        {
                                            try
                                            {
                                                client.GetStream().Write(data, 0, data.Length);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Broadcast error for {code}: {ex.Message}");
                            }
                        }

                        Thread.Sleep(60000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Broadcast loop error: " + ex.Message);
                        Thread.Sleep(60000);
                    }
                }
            })
            { IsBackground = true }.Start();
        }
    }
}
