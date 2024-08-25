using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using StClientLib; // SmartCom broker library for stock exchange connectivity (https://d8.capital/, ex-"IT-invest")

namespace TradeConnect
{
    class Trader2
    {
        private Quote LastQuote;
        private StServerClass SmartServer;
        private List<Tiker> InfoTikers;
        private List<string> InfoTypes;
        private List<Bar> InfoBars;
        private List<string> InfoPortfolios;
        private StreamWriter tw;
        private StreamWriter twclose;
        private StreamWriter twopen;

        private Stopwatch sw1;

        private int entrycount = 0;

        //constants taken from config file
        int posLimitMval = 200;
        int negLimitMval = -200;
        int posLimitZscr = 2;
        int negLimitZscr = -2;
        int tradeDuration = 10;

        int terminateThread = 0;
        int serverHour;
        int serverMin;
        int serverSec;

        int closeFlag = 0;
        int openFlag = 0;
        int closeReady = 1;
        int openReady = 1;

        int buyDone = 0;
        int sellDone = 0;

        int userMachineFaster = 1; // assume that local time runs faster than server time, because if the first tick arrives too late
                                   // we will not know the timing difference and in that case it is better to
                                   // make such an assumption since it will protect us in any case, although at some cost
                                   // (if local time runs faster or equal, we lose 5min - [actual difference] time)
                                   // (if local time runs slower, we lose 5 min + [actual difference] time)
        int deltaDef = 0;
        int deltaDef2 = 0;
        int deltaClose = 0;

        double BuySpread_SBRF_SBER = 0;
        double SellSpread_SBRF_SBER = 0;

        double BuySpread_613_913 = 0;
        double SellSpread_613_913 = 0;

        double currBestBid_RTS613 = -1;
        double currBestAsk_RTS613 = -1;
        double currBestBid_RTS913 = -1;
        double currBestAsk_RTS913 = -1;
        double currBestBid_SBER = -1;
        double currBestAsk_SBER = -1;
        double currBestBid_SBRF = -1;
        double currBestAsk_SBRF = -1;

        int BuyUpdatedRTS = 0;
        int SellUpdatedRTS = 0;
        int BuyUpdatedSB = 0;
        int SellUpdatedSB = 0;

        static int cookieInit = 432198765;

        private void doActions()
        {
            try
            {
                SmartServer = new StServerClass(); // initialize broker connection
                InfoTypes = new List<string>();
                InfoTikers = new List<Tiker>();
                InfoBars = new List<Bar>();
                InfoPortfolios = new List<string>();

                sw1 = new Stopwatch();

                Listeners listeners = new Listeners(
                        posLimitMval,
                        negLimitMval,
                        posLimitZscr,
                        negLimitZscr,
                        tradeDuration,
                        LastQuote,
                        SmartServer,
                        InfoTikers,
                        InfoTypes,
                        InfoBars,
                        InfoPortfolios,
                        tw,
                        twclose,
                        twopen,
                        sw1
                );
                SmartServer.AddSymbol += new _IStClient_AddSymbolEventHandler(listeners.smartServer_AddSymbol);                /* add listener for AddSymbol events      */
                SmartServer.UpdateQuote += new _IStClient_UpdateQuoteEventHandler(listeners.smartServer_UpdateQuote);          /* add listener for UpdateQuote events    */
                SmartServer.AddTick += new _IStClient_AddTickEventHandler(listeners.smartServer_AddTick);                      /* add listener for AddTick events        */
                SmartServer.UpdateBidAsk += new _IStClient_UpdateBidAskEventHandler(listeners.smartServer_UpdateBidAsk);       /* add listener for UpdateBidAsk events   */
                SmartServer.AddBar += new _IStClient_AddBarEventHandler(listeners.smartServer_AddBar);                         /* add listener for AddBar events         */
                SmartServer.AddPortfolio += new _IStClient_AddPortfolioEventHandler(listeners.smartServer_AddPortfolio);       /* add listener for AddPortfolio events   */
                SmartServer.SetPortfolio += new _IStClient_SetPortfolioEventHandler(listeners.smartServer_SetPortfolio);       /* add listener for SetPortfolio events   */
                SmartServer.OrderSucceeded += new _IStClient_OrderSucceededEventHandler(listeners.smartServer_OrderSucceeded); /* add listener for OrderSucceeded events */
                SmartServer.OrderFailed += new _IStClient_OrderFailedEventHandler(listeners.smartServer_OrderFailed);          /* add listener for OrderFailed events    */

                File.Delete("stocksharplog.txt");
                File.Delete("RTS-6.13_FT.txt");
                File.Delete("BidAskSpread_Updates.txt");
                File.Delete("RTSo-6.13_FT.txt");
                File.Delete("RTSc-6.13_FT.txt");

                try
                {
                    do
                    {
                        SmartServer.connect("82.204.220.34", 8090, "******", "Synterobot");   /* connect to broker */
                    }
                    while (!SmartServer.IsConnected()); // wait until connection is complete

                    if (SmartServer.IsConnected()) // proceed once connected
                    {
                        Console.WriteLine("Connection established.");
                        try
                        {
                            terminateThread = 1;
                            new Thread(ConfigReaderRoutine).Start();

                            SmartServer.GetPortfolioList();              /*get list of portfolios available for current login */
                            SmartServer.ListenPortfolio("BP12829-RF-02"); /*receive notifications about portfolio changes      */
                            SmartServer.GetSymbols();                     /* get symbols list                                  */

                            //     we need to get two bars for last two hours every hour at xx.00.00 and 
                            //     o0 <= open  value of the very last bar 
                            //     o1 <= open  value of the bar before the very last bar
                            //     c1 <= close value of the bar before the very last bar
                            //     and then apply the suggested algorithm

                            //     hence 
                            //     the first  invocation of AddBar should fill in o0  
                            // and the second invocation of AddBar should fill in o1 and c1

                            SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);

                            ListenSymbol("RTS-6.13_FT"); /* subscribe for ticks, bids & quotes */
                            ListenSymbol("RTS-6.13_FT");
                            ListenSymbol("SBRF-6.13_FT");
                            ListenSymbol("SBER");

                            new Thread(DefinePreciseClose).Start();
                            new Thread(DefinePreciseOpen) .Start();

                            //     this thread is going to detect xx.59.59 and revert order if necessary
                            new Thread(InvertPlaceOrder).Start();
                            //     current thread is going to detect time xx.00.00                                                          

                            while (true)
                            {
                                if (userMachineFaster == 1) // fetch bars 5 minuntes after xx.00.00
                                    deltaDef = 11;
                                else                        // fetch bars right at xx.00.00
                                    deltaDef = 12;

                                if (DateTime.Now.Minute == (60 - 5 * deltaDef) && DateTime.Now.Second == 00)
                                {
                                    SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);
                                }

                                Thread.Sleep(50);
                            }

                            Console.ReadKey();
                            SmartServer.disconnect(); /* disconnect once we receive everything we need */
                        }
                        catch (Exception Error)
                        {
                            Console.WriteLine("Error occurred when getting symbols list from server");
                        }
                    }
                }
                catch (Exception Error)
                {
                    Console.WriteLine("Error occurred when connecting to SmartCom server");
                }
            }
            catch (Exception Error)
            {
                Console.WriteLine("Error occurred when creating SmartCom object");
            }
        }

        private void UpDateQuote() //supposed to change GUI labels. Not needed in console application
        {
              if (LastQuoteLabel.InvokeRequired)
                  LastQuoteLabel.BeginInvoke(new System.Windows.Forms.MethodInvoker(UpDateQuote));
              else
              {
                  LastAskLabel.Text = "Ask: " + LastQuote.Ask + " (" + LastQuote.AskVolume + ")";
                  LastBidLabel.Text = "Bid: " + LastQuote.Bid + " (" + LastQuote.BidVolume + ")";
                  LastLabel.Text = LastQuote.LastClock.ToLongTimeString() + " " +
                                   LastQuote.LastPrice + " (" + LastQuote.LastVolume + ") -> " +
                                   (LastQuote.LastAction == StOrder_Action.StOrder_Action_Buy ? "B" : LastQuote.LastAction == StOrder_Action.StOrder_Action_Sell ? "S" : LastQuote.LastAction.ToString());
                  LastQuoteLabel.Text = "Status: " + LastQuote.Status;
              }
        }

        private void ListenSymbol(string Code)
        {
            if (SmartServer.IsConnected())
            {
                try
                {
                    Console.WriteLine("[" + Code + "] subscribed for Ticks ", "log", "{0} Listen: {1}, {2}", DateTime.Now, "Ticks", Code);
                    SmartServer.ListenTicks(Code);
                    try
                    {
                        Console.WriteLine("[" + Code + "] subscribed for BidAsks", "log", "{0} Listen: {1}, {2}", DateTime.Now, "BidAsks", Code);
                        SmartServer.ListenBidAsks(Code);
                        try
                        {
                            Console.WriteLine("[" + Code + "] subscribed for Quotes", "log", "{0} Listen: {1}, {2}", DateTime.Now, "Quotes", Code);
                            SmartServer.ListenQuotes(Code);
                        }
                        catch (Exception Error)
                        {
                            Console.WriteLine("Error on subscribing for Ticks");
                        }
                    }
                    catch (Exception Error)
                    {
                        Console.WriteLine("Error on subscribing for BidAsks");
                    }
                }
                catch (Exception Error)
                {
                    Console.WriteLine("Error on subscribing for Quotes");
                }
            }
        }

        private void DefinePreciseClose() //this thread starts checking close values right at xx.59.59
        {                                 //it checks out close value every 100 milliseconds
            File.Delete("CloseValueCheckouts.txt");

            while (true)                          //so that we have total of 10 checkouts per second
            {
                if (DateTime.Now.Minute == 59 && DateTime.Now.Second == 59)
                {
                    closeFlag = 1; //so the file above is populated

                    twclose = File.AppendText("CloseValueCheckouts.txt");  //open once
                    twclose.WriteLine("\n--------TIME: " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);

                    while (DateTime.Now.Second == 59)
                    {
                        closeReady = 0;
                        SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 1);  //the current bar
                        Thread.Sleep(90);
                    }

                    twclose.Close();
                    closeFlag = 0;//reset
                }
            }
        }

        private void DefinePreciseOpen() //this thread checks open value right at xx.00.00
        {
            File.Delete("OpenValueCheckouts.txt");

            while (true)
            {

                if (DateTime.Now.Minute == 00 && DateTime.Now.Second == 00)
                {
                    openFlag = 1; //so the file above is populated

                    twopen = File.AppendText("OpenValueCheckouts.txt");
                    twopen.WriteLine("\n--------TIME: " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);

                    openReady = 0;
                    SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 1);  //get the last bar as early as possible

                    twopen.Close();
                    openFlag = 0;//reset
                }
            }
        }

        private void InvertPlaceOrder()
        {
            while (true)
            {
                if (userMachineFaster == 1)                // trade close time right at 18.44.59
                {
                    deltaClose = 0;
                    deltaDef2 = 12;
                }
                else                                       // trade close time 5 minutes before 18.44.59
                {
                    deltaClose = -5;
                    deltaDef2 = 11;
                }

                if (DateTime.Now.Minute == (5 * deltaDef2 - 1) && DateTime.Now.Second == 59 ||
                    DateTime.Now.Hour == 18 && DateTime.Now.Minute == (44 + deltaClose) && DateTime.Now.Second == 59)
                {
                    tw = File.AppendText("RTS-6.13_FT.txt");

                    if (buyDone == 1)
                    {
                        //sell

                        SmartServer.PlaceOrder(
                            "BP12829-RF-02",
                            "RTS-6.13_FT",
                            StOrder_Action.StOrder_Action_Sell,
                            StOrder_Type.StOrder_Type_Market,
                            StOrder_Validity.StOrder_Validity_Day,
                            0,
                            1,
                            0,
                            cookieInit++
                        );

                        Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] SELL ORDER PLACED");
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] SELL ORDER PLACED");

                        buyDone = 0;  //reset                        
                    }

                    if (sellDone == 1)
                    {
                        //buy

                        SmartServer.PlaceOrder(
                            "BP12829-RF-02",
                            "RTS-6.13_FT",
                            StOrder_Action.StOrder_Action_Buy,
                            StOrder_Type.StOrder_Type_Market,
                            StOrder_Validity.StOrder_Validity_Day,
                            0,
                            1,
                            0,
                            cookieInit++
                        );

                        Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] BUY ORDER PLACED");
                        tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] BUY ORDER PLACED");

                        sellDone = 0; //reset
                    }

                    tw.Close();
                }

                Thread.Sleep(50);
            }
        }

        private void TradeBuyThreadRoutine(object data)
        {
            //created for every trade and lasts a number of minutes specified in config file (tradeDuration)

            SmartServer.PlaceOrder(
                "BP8602-RF-02",
                "RTS-6.13_FT",
                StOrder_Action.StOrder_Action_Buy,
                StOrder_Type.StOrder_Type_Market,
                StOrder_Validity.StOrder_Validity_Day,
                0,
                1,
                0,
                cookieInit++
            );

            int startCount = (int)data;

            while (ultimateMinCounter - startCount < tradeDuration)
            {
                if (serverHour == 18 && serverMin == 59 && serverSec > 30)
                    break;
                Thread.Sleep(100);
            }

            SmartServer.PlaceOrder(
                "BP8602-RF-02",
                "RTS-6.13_FT",
                StOrder_Action.StOrder_Action_Sell,
                StOrder_Type.StOrder_Type_Market,
                StOrder_Validity.StOrder_Validity_Day,
                0,
                1,
                0,
                cookieInit++
            );
        }

        private void TradeSellThreadRoutine(object data)
        {
            //created for every trade and lasts a number of minutes specified in config file (tradeDuration)

            SmartServer.PlaceOrder(
                "BP8602-RF-02",
                "RTS-6.13_FT",
                StOrder_Action.StOrder_Action_Sell,
                StOrder_Type.StOrder_Type_Market,
                StOrder_Validity.StOrder_Validity_Day,
                0,
                1,
                0,
                cookieInit++
            );

            int startCount = (int)data;

            while (ultimateMinCounter - startCount < tradeDuration)
            {
                if (serverHour == 18 && serverMin == 59 && serverSec > 30)
                    break;
                Thread.Sleep(100);
            }

            SmartServer.PlaceOrder(
                "BP8602-RF-02",
                "RTS-6.13_FT",
                StOrder_Action.StOrder_Action_Buy,
                StOrder_Type.StOrder_Type_Market,
                StOrder_Validity.StOrder_Validity_Day,
                0,
                1,
                0,
                cookieInit++
            );
        }//end routine


        private void ConfigReaderRoutine()
        {
            //created at the beginning of OnStarted and continuosly reads config file values in an infinite loop

            while (terminateThread == 1)
            {
                System.IO.StreamReader exfile = new System.IO.StreamReader("conf.ini");
                exfile.ReadLine();
                posLimitMval = Convert.ToInt32(exfile.ReadLine());
                exfile.ReadLine();
                negLimitMval = Convert.ToInt32(exfile.ReadLine());
                exfile.ReadLine();
                posLimitZscr = Convert.ToInt32(exfile.ReadLine());
                exfile.ReadLine();
                negLimitZscr = Convert.ToInt32(exfile.ReadLine());
                exfile.ReadLine();
                tradeDuration = Convert.ToInt32(exfile.ReadLine());
                exfile.ReadLine();
                neededCloseNumber = Convert.ToInt32(exfile.ReadLine());
                exfile.Close();

                Console.WriteLine("config info: "
                    + posLimitMval + " "
                    + negLimitMval + " " +
                    posLimitZscr + " " +
                    negLimitZscr + " " +
                    tradeDuration + " " +
                    neededCloseNumber
                );

                Thread.Sleep(5000);
            }
        }

        static void Main(string[] args)
        {
            Trader2 ntc = new Trader2();
            ntc.doActions();
        }
    }
}