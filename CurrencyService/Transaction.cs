using System;
using System.Runtime.Serialization;

namespace CurrencyService
{
    [DataContract]
    public class Transaction
    {
        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public string Type { get; set; }

        [DataMember]
        public string CurrencyCode { get; set; }

        [DataMember]
        public double Amount { get; set; }

        [DataMember]
        public double Rate { get; set; }

        [DataMember]
        public DateTime Date { get; set; }
    }
}
