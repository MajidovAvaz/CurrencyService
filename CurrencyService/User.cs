using System.Collections.Generic;

namespace CurrencyService
{
    public class User
    {
        public string Username { get; set; }
        public double Balance { get; set; }

    
        public Dictionary<string, double> OwnedCurrencies { get; set; }

        public User(string username)
        {
            Username = username;
            Balance = 0.0;
            OwnedCurrencies = new Dictionary<string, double>();
        }
    }
}
