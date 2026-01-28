using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text.Json;

namespace CurrencyService
{
    public class CurrencyService : ICurrencyService
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CurrencyDB"].ConnectionString;

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public bool Login(string username, string password)
        {
            string hashedPassword = HashPassword(password);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT PasswordHash FROM Users WHERE Username = @Username", conn);
                cmd.Parameters.AddWithValue("@Username", username);
                var result = cmd.ExecuteScalar();

                if (result == null) return false;
                return result.ToString() == hashedPassword;
            }
        }

        public string GetMessage(string name)
        {
            return $"Hello, {name}! Welcome to the Currency Exchange Service.";
        }

        public double GetExchangeRate(string currencyCode)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"http://api.nbp.pl/api/exchangerates/rates/A/{currencyCode}/?format=json";
                    var response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();

                    var json = response.Content.ReadAsStringAsync().Result;

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        return doc.RootElement.GetProperty("rates")[0].GetProperty("mid").GetDouble();
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        public double GetHistoricalExchangeRate(string currencyCode, string date)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"http://api.nbp.pl/api/exchangerates/rates/A/{currencyCode}/{date}/?format=json";
                    var response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();

                    var json = response.Content.ReadAsStringAsync().Result;

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        return doc.RootElement.GetProperty("rates")[0].GetProperty("mid").GetDouble();
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        public bool CreateAccount(string username, string password)
        {
            string hashedPassword = HashPassword(password);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @Username", conn);
                checkCmd.Parameters.AddWithValue("@Username", username);

                int exists = (int)checkCmd.ExecuteScalar();
                if (exists > 0) return false;

                var insertCmd = new SqlCommand("INSERT INTO Users (Username, PasswordHash, Balance) VALUES (@Username, @PasswordHash, 0)", conn);
                insertCmd.Parameters.AddWithValue("@Username", username);
                insertCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                insertCmd.ExecuteNonQuery();
                return true;
            }
        }

        public bool TopUpAccount(string username, double amount)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var updateCmd = new SqlCommand("UPDATE Users SET Balance = Balance + @Amount WHERE Username = @Username", conn);
                updateCmd.Parameters.AddWithValue("@Username", username);
                updateCmd.Parameters.AddWithValue("@Amount", amount);
                return updateCmd.ExecuteNonQuery() > 0;
            }
        }

        public double GetBalance(string username)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Balance FROM Users WHERE Username = @Username", conn);
                cmd.Parameters.AddWithValue("@Username", username);

                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToDouble(result) : -1;
            }
        }

        public bool BuyCurrency(string username, string currencyCode, double amount)
        {
            double rate = GetExchangeRate(currencyCode);
            if (rate <= 0) return false;
            double cost = rate * amount;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    var checkBalanceCmd = new SqlCommand("SELECT Balance FROM Users WHERE Username = @Username", conn, transaction);
                    checkBalanceCmd.Parameters.AddWithValue("@Username", username);
                    double balance = Convert.ToDouble(checkBalanceCmd.ExecuteScalar());
                    if (balance < cost) return false;

                    var deductCmd = new SqlCommand("UPDATE Users SET Balance = Balance - @Cost WHERE Username = @Username", conn, transaction);
                    deductCmd.Parameters.AddWithValue("@Username", username);
                    deductCmd.Parameters.AddWithValue("@Cost", cost);
                    deductCmd.ExecuteNonQuery();

                    var upsertCmd = new SqlCommand(@"
                        MERGE CurrencyHoldings AS target
                        USING (SELECT @Username AS Username, @CurrencyCode AS CurrencyCode) AS source
                        ON target.Username = source.Username AND target.CurrencyCode = source.CurrencyCode
                        WHEN MATCHED THEN
                            UPDATE SET Amount = Amount + @Amount
                        WHEN NOT MATCHED THEN
                            INSERT (Username, CurrencyCode, Amount) VALUES (@Username, @CurrencyCode, @Amount);", conn, transaction);

                    upsertCmd.Parameters.AddWithValue("@Username", username);
                    upsertCmd.Parameters.AddWithValue("@CurrencyCode", currencyCode);
                    upsertCmd.Parameters.AddWithValue("@Amount", amount);
                    upsertCmd.ExecuteNonQuery();

                    var insertTx = new SqlCommand("INSERT INTO Transactions (Username, Type, CurrencyCode, Amount, Rate) VALUES (@Username, 'Buy', @CurrencyCode, @Amount, @Rate)", conn, transaction);
                    insertTx.Parameters.AddWithValue("@Username", username);
                    insertTx.Parameters.AddWithValue("@CurrencyCode", currencyCode);
                    insertTx.Parameters.AddWithValue("@Amount", amount);
                    insertTx.Parameters.AddWithValue("@Rate", rate);
                    insertTx.ExecuteNonQuery();

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        public bool SellCurrency(string username, string currencyCode, double amount)
        {
            double rate = GetExchangeRate(currencyCode);
            if (rate <= 0) return false;
            double income = rate * amount;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    var checkCurrencyCmd = new SqlCommand("SELECT Amount FROM CurrencyHoldings WHERE Username = @Username AND CurrencyCode = @CurrencyCode", conn, transaction);
                    checkCurrencyCmd.Parameters.AddWithValue("@Username", username);
                    checkCurrencyCmd.Parameters.AddWithValue("@CurrencyCode", currencyCode);

                    object result = checkCurrencyCmd.ExecuteScalar();
                    if (result == null || Convert.ToDouble(result) < amount)
                        return false;

                    var updateHoldingsCmd = new SqlCommand("UPDATE CurrencyHoldings SET Amount = Amount - @Amount WHERE Username = @Username AND CurrencyCode = @CurrencyCode", conn, transaction);
                    updateHoldingsCmd.Parameters.AddWithValue("@Username", username);
                    updateHoldingsCmd.Parameters.AddWithValue("@CurrencyCode", currencyCode);
                    updateHoldingsCmd.Parameters.AddWithValue("@Amount", amount);
                    updateHoldingsCmd.ExecuteNonQuery();

                    var addBalanceCmd = new SqlCommand("UPDATE Users SET Balance = Balance + @Income WHERE Username = @Username", conn, transaction);
                    addBalanceCmd.Parameters.AddWithValue("@Income", income);
                    addBalanceCmd.Parameters.AddWithValue("@Username", username);
                    addBalanceCmd.ExecuteNonQuery();

                    var insertTx = new SqlCommand("INSERT INTO Transactions (Username, Type, CurrencyCode, Amount, Rate) VALUES (@Username, 'Sell', @CurrencyCode, @Amount, @Rate)", conn, transaction);
                    insertTx.Parameters.AddWithValue("@Username", username);
                    insertTx.Parameters.AddWithValue("@CurrencyCode", currencyCode);
                    insertTx.Parameters.AddWithValue("@Amount", amount);
                    insertTx.Parameters.AddWithValue("@Rate", rate);
                    insertTx.ExecuteNonQuery();

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }

        public List<Transaction> GetTransactionHistory(string username)
        {
            List<Transaction> history = new List<Transaction>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Type, CurrencyCode, Amount, Rate, Date FROM Transactions WHERE Username = @Username ORDER BY Date DESC", conn);
                cmd.Parameters.AddWithValue("@Username", username);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        history.Add(new Transaction
                        {
                            Username = username,
                            Type = reader.GetString(0),
                            CurrencyCode = reader.GetString(1),
                            Amount = reader.GetDouble(2),
                            Rate = reader.GetDouble(3),
                            Date = reader.GetDateTime(4)
                        });
                    }
                }
            }

            return history;
        }

        public Dictionary<string, double> GetUserCurrencies(string username)
        {
            Dictionary<string, double> holdings = new Dictionary<string, double>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT CurrencyCode, Amount FROM CurrencyHoldings WHERE Username = @Username", conn);
                cmd.Parameters.AddWithValue("@Username", username);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string currency = reader.GetString(0);
                        double amount = reader.GetDouble(1);
                        holdings[currency] = amount;
                    }
                }
            }

            return holdings;
        }

        public bool VirtualPayment(string fromUser, string toUser, double amount)
        {
            if (amount <= 0) return false;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    var checkSender = new SqlCommand("SELECT Balance FROM Users WHERE Username = @FromUser", conn, transaction);
                    checkSender.Parameters.AddWithValue("@FromUser", fromUser);
                    object senderResult = checkSender.ExecuteScalar();
                    if (senderResult == null || Convert.ToDouble(senderResult) < amount)
                        return false;

                    var checkReceiver = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username = @ToUser", conn, transaction);
                    checkReceiver.Parameters.AddWithValue("@ToUser", toUser);
                    int receiverExists = (int)checkReceiver.ExecuteScalar();
                    if (receiverExists == 0)
                        return false;

                    var deductCmd = new SqlCommand("UPDATE Users SET Balance = Balance - @Amount WHERE Username = @FromUser", conn, transaction);
                    deductCmd.Parameters.AddWithValue("@Amount", amount);
                    deductCmd.Parameters.AddWithValue("@FromUser", fromUser);
                    deductCmd.ExecuteNonQuery();

                    var addCmd = new SqlCommand("UPDATE Users SET Balance = Balance + @Amount WHERE Username = @ToUser", conn, transaction);
                    addCmd.Parameters.AddWithValue("@Amount", amount);
                    addCmd.Parameters.AddWithValue("@ToUser", toUser);
                    addCmd.ExecuteNonQuery();

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    return false;
                }
            }
        }
    }
}