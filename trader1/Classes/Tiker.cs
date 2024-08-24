using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using StClientLib; // broker library for stock exchange connectivity (https://d8.capital/)
using System.Data.Odbc;

namespace TradeConnect
{
    public class Tiker
    {
        private string sCode;
        private string sShortName;
        private string sLongName;
        private double dStep;
        private double dStepPrice;
        private int iDecimals;
        private string sSecExtId;
        private string sSecExchName;
        private System.DateTime dtExpiryDate;
        private double dDaysBeforeExpiry;
        private double dStrike;

        public Tiker(
                string code,
                string shortname,
                string longname,
                double step,
                double stepprice,
                double decimals,
                string sec_ext_id,
                string sec_exch_name,
                System.DateTime expiryDate,
                double daysbeforeexpiry,
                double strike
        )
        {
            sCode = code;
            sShortName = shortname;
            sLongName = longname;
            dStep = step;
            dStepPrice = stepprice;
            iDecimals = (int)decimals;
            sSecExtId = sec_ext_id;
            sSecExchName = sec_exch_name;
            dtExpiryDate = expiryDate;
            dDaysBeforeExpiry = daysbeforeexpiry;
            dStrike = strike;
        }

        public double ToMoney(double Punkts)
        {
            return dStepPrice / dStep * Punkts;
        }

        public override string ToString()
        {
            CultureInfo ci = new CultureInfo("en-us");
            return "[symbol = " + sCode +
            "]\n[strike = " + dStrike +
            "]\n[Punkt = " + dStepPrice +
            "]\n[Step = " + dStep.ToString("G", ci) +
            "]\n[Decimals = " + iDecimals +
            "]\n[Money = " + ToMoney(1) +
            "]\n[shortname = " + sShortName +
            "]\n[expirydate = " + dtExpiryDate.ToShortDateString() +
            "] (" + (int)dDaysBeforeExpiry + " days before expiry)";
        }

        public string Code { get { return sCode; } }
        public string ShortName { get { return sShortName; } }
        public string LongName { get { return sLongName; } }
        public double Step { get { return dStep; } }
        public double StepPrice { get { return dStepPrice; } }
        public int Decimals { get { return iDecimals; } }
        public string SecExtId { get { return sSecExtId; } }
        public string SecExchName { get { return sSecExchName; } }
        public System.DateTime ExpiryDate { get { return dtExpiryDate; } }
        public double DaysBeforeExpiry { get { return dDaysBeforeExpiry; } }
    }
}