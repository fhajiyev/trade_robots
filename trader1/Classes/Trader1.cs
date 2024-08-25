using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using StClientLib; // SmartCom broker library for stock exchange connectivity (https://d8.capital/, ex-"IT-invest")
using System.Data.Odbc; // ODBC for connecting to MS Access

namespace TradeConnect
{
    class Trader
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

        int closeFlag = 0;
        int openFlag = 0;
        int closeReady = 1;
        int openReady = 1;

        int buyDone = 0;
        int sellDone = 0;

        int userMachineFaster = 0;

        int deltaDef = 0;
        int deltaClose = 0;

        int minuteOpen = 0;
        int entryCount;
        int accumulator;
        int latestMinute;
        string latestID = "0";

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

                File.Delete("RTS-6.13_FT.txt");

                try
                {
                    do
                    {
                        SmartServer.connect("82.204.220.34", 8090, "******", "robotrade"); /* connect to broker */
                    }
                    while (!SmartServer.IsConnected()); // wait until connection is complete

                    if (SmartServer.IsConnected()) // proceed once connected
                    {
                        Console.WriteLine("Connection established");
                        try
                        {
                            SmartServer.GetPortfolioList();               /* get list of portfolios available for current user  */
                            SmartServer.ListenPortfolio("BP12829-RF-02"); /* receive notifications about portfolio changes      */
                            SmartServer.GetSymbols();                     /* get symbols list                                   */

                            //     we need to get two bars for last two hours every hour at xx.00.00 and 
                            //     o0 <= open  value of the very last bar 
                            //     o1 <= open  value of the bar before the very last bar
                            //     c1 <= close value of the bar before the very last bar
                            //     and then apply the suggested algorithm

                            //     hence 
                            //     the first  invocation of AddBar should fill in o0  
                            //     and the second invocation of AddBar should fill in o1 and c1

                            SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);

                            ListenSymbol("RTS-6.13_FT");   /* subscribe for ticks, bids & quotes of the specified symbol */                                 */
                            new Thread(DefinePreciseClose).Start();
                            new Thread(DefinePreciseOpen) .Start();

                            //     this thread is going to detect xx.59.59 and revert order if necessary
                            new Thread(InvertPlaceOrder).Start();
                            //     current thread is going to detect time xx.00.00

                            try
                            {
                                // ODBC is needed for reading data from MS Access document
                                OdbcConnection myconn = new OdbcConnection();                                                             

                                myconn.ConnectionString = @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};" +
                                                           "Dbq=C:\\expdata.accdb";
                                myconn.Open();
                                OdbcDataReader dr;
                                OdbcCommand cmd = myconn.CreateCommand();
                                string maxNum = "0";
                                entryCount = 0;
                                DateTime dt;
                                string amountBuySell = "0";

                                sw1.Start();

                                while (true)
                                {
                                    cmd.CommandText = "Select MAX(Номер) from все_сделки"; // get the number of the latest entry
                                    dr = cmd.ExecuteReader();

                                    dr.Read();
                                    maxNum = dr[0].ToString();
                                    dr.Close();

                                    cmd.CommandText = "Select * from все_сделки where Номер = " + maxNum; //get latest entry
                                    dr = cmd.ExecuteReader();

                                    dr.Read();

                                    // parse time here

                                    dt = DateTime.ParseExact(dr["Время"].ToString(), "h:mm:sstt", CultureInfo.InvariantCulture);
                                    amountBuySell = dr["Кол_во"].ToString();
                                    
                                    dr.Close();

                                    if (maxNum != latestID)
                                    {
                                        Console.WriteLine(maxNum);
                                        latestID = maxNum;
                                    }

                                    sw1.Stop();
                                    Console.WriteLine("\nelapsed millis: " + sw1.ElapsedMilliseconds);
                                    sw1.Reset();

                                    // aggregate sales/buys per minute and write into log

                                    if (entryCount == 0) // save the minute at launch, so we should skip it and start logging at next minute
                                    {
                                        minuteOpen = dt.Minute;
                                        latestMinute = dt.Minute;
                                    }

                                    if (dt.Minute != minuteOpen) //if the minute at launch has finished we start logging
                                    {
                                        //if current minute is different from latest minute, we reset accumulator and
                                        //change latest minute

                                        if (dt.Minute != latestMinute)
                                        {
                                            //save down the last minute log and reset accumulator

                                            if (latestMinute != minuteOpen) //do not output anything for the minute at launch
                                            {
                                                Console.WriteLine("\n[" + DateTime.Now.AddHours(-1).AddMinutes(-1).Hour + ":" + DateTime.Now.AddHours(-1).AddMinutes(-1).Minute + ":" + "00] accumulated amount for this minute: " + accumulator);
                                            }

                                            accumulator = Convert.ToInt32(amountBuySell);
                                            Console.WriteLine(accumulator);

                                            latestMinute = dt.Minute;
                                        }

                                        else
                                        {
                                            //we are still in the current minute so we increment accumulator

                                            if (latestID != maxNum)  // loop frequency may be faster than incoming message frequency of QUIK
                                                                     // so we avoid counting the same entry twice
                                            {
                                                accumulator = accumulator + Convert.ToInt32(amountBuySell);
                                                Console.WriteLine(amountBuySell);
                                            }
                                        }
                                    }

                                    latestID = maxNum;
                                    entryCount++;
                                }

                                Console.ReadKey();
                                myconn.Close();

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("\nodbc error occurred. " + e.Message); Console.ReadKey();
                            }

                            while (true)
                            {
                                if (userMachineFaster == 1) // fetch bars 5 minuntes after xx.00.00
                                    deltaDef = 11;
                                else                        // fetch bars 5 minutes before xx.00.00
                                    deltaDef = 1;

                                if (DateTime.Now.Minute == (60 - 5 * deltaDef) && DateTime.Now.Second == 00)
                                {

                                    SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 2);

                                }//end if

                                Thread.Sleep(50);

                            }//end while                      

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

        private void DefinePreciseClose()
        {   //this thread starts checking close values right at xx.59.59
            //it checks out close value every 100 milliseconds

            File.Delete("CloseValueCheckouts.txt");

            while (true) //so that we have total of 10 checkouts per second
            {
                if (DateTime.Now.Minute == 59 && DateTime.Now.Second == 59)
                {
                    closeFlag = 1; //so the file above is populated

                    twclose = File.AppendText("CloseValueCheckouts.txt"); //open once
                    twclose.WriteLine("\n--------TIME: " + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second);

                    while (DateTime.Now.Second == 59)
                    {
                        closeReady = 0;
                        SmartServer.GetBars("RTS-6.13_FT", StBarInterval.StBarInterval_60Min, DateTime.Now, 1);  //the current bar
                        Thread.Sleep(90);
                    }

                    twclose.Close();
                    closeFlag = 0; //reset
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
                    openFlag = 0; //reset
                }
            }
        }

        private void InvertPlaceOrder()
        {
            while (true)
            {
                if (userMachineFaster == 1) // trade close time 5 minuntes after 18.44.59
                    deltaClose = 5;
                else                        // trade close time 5 minutes before 18.44.59
                    deltaClose = -5;

                if (DateTime.Now.Minute == (60 - 5 * deltaDef - 1) && DateTime.Now.Second == 59 ||
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

        static void Main(string[] args)
        {
            Trader ntc = new Trader();
            ntc.doActions();
        }
    }
}