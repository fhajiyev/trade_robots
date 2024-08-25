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
    public class Quote
    {
        public event EventHandler EventUpDate;
        public delegate void EventHandler();

        private string InfoCode;
        private double InfoAsk;
        private double InfoBid;
        private int InfoAskVolume;
        private int InfoBidVolume;
        private int InfoStatus;
        private long InfoLastNo;
        private double InfoLastPrice;
        private int InfoLastVolume;
        private DateTime InfoLastClock;
        private StClientLib.StOrder_Action InfoLastAction;

        public Quote(
                string Code,
                DateTime Clock,
                double Last,
                double Volume,
                int Status,
                EventHandler OnEventUpDate
        )
        {
            InfoCode = Code;
            InfoAsk = 0.0d;
            InfoBid = 0.0d;
            InfoLastNo = 0;
            InfoAskVolume = 0;
            InfoBidVolume = 0;
            InfoStatus = Status;
            InfoLastPrice = Last;
            InfoLastVolume = (int)Volume;
            InfoLastClock = Clock;
            if (OnEventUpDate != null)
                EventUpDate += new EventHandler(OnEventUpDate);
            new Thread(ThreadUpdate).Start();
        }

        private void ThreadUpdate()
        {
            if (EventUpDate != null)
                EventUpDate();
        }

        public void UpDate(int Status)
        {
            InfoStatus = Status;
            new Thread(ThreadUpdate).Start();
        }
        public void UpDate(double Ask, double AskVolume, double Bid, double BidVolume)                                     /* used by SmartServer_UpdateBidAsk listener */
        {
            InfoAsk = Ask;
            InfoBid = Bid;
            InfoAskVolume = (int)AskVolume;
            InfoBidVolume = (int)BidVolume;
            new Thread(ThreadUpdate).Start();
        }
        public void UpDate(DateTime Clock, double Price, double Volume, long LastNo, StClientLib.StOrder_Action Action)    /* used by SmartServer_AddTick listener     */
        {
            InfoLastNo = LastNo;
            InfoLastClock = Clock;
            InfoLastPrice = Price;
            InfoLastVolume = (int)Volume;
            InfoLastAction = Action;
            new Thread(ThreadUpdate).Start();
        }


        public string Code { get { return InfoCode; } }
        public double Ask { get { return InfoAsk; } }
        public double Bid { get { return InfoBid; } }
        public int AskVolume { get { return InfoAskVolume; } }
        public int BidVolume { get { return InfoBidVolume; } }
        public int Status { get { return InfoStatus; } }
        public long LastNo { get { return InfoLastNo; } }
        public double LastPrice { get { return InfoLastPrice; } }
        public int LastVolume { get { return InfoLastVolume; } }
        public DateTime LastClock { get { return InfoLastClock; } }
        public StClientLib.StOrder_Action LastAction { get { return InfoLastAction; } }
    }
}