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
    class Listeners {
    
            public void smartServer_AddSymbol(
                    int row, 
                    int nrows, 
                    string symbol, 
                    string short_name, 
                    string long_name, 
                    string type, 
                    int decimals, 
                    int lot_size, 
                    double punkt, 
                    double step, 
                    string sec_ext_id, 
                    string sec_exch_name, 
                    System.DateTime expiry_date, 
                    double days_before_expiry, 
                    double strike
            )
            {
                InfoTikers.Add(new Tiker(symbol, short_name, long_name, step, punkt, decimals, sec_ext_id, sec_exch_name, expiry_date, days_before_expiry, strike));
    
                if (symbol == "RTS-6.13_FT" || symbol == "RTSo-6.13_FT" || symbol == "RTSc-6.13_FT")
                {
                    tw = File.AppendText(symbol + ".txt");
    
                    Console.WriteLine("\n[SmartServer_AddSymbol message]: symbol detected");
                    tw.WriteLine("\n[SmartServer_AddSymbol message]: symbol detected");
                    Console.WriteLine(InfoTikers[InfoTikers.Count - 1].ToString()); /* which implies the last added element */
                    tw.WriteLine(InfoTikers[InfoTikers.Count - 1].ToString());
                    tw.Close();
                }
   
                if (InfoTypes.IndexOf(type) == -1)
                    InfoTypes.Add(type); 
            }
    
            public void smartServer_UpdateQuote(
                    string symbol, 
                    System.DateTime datetime, 
                    double open, 
                    double high, 
                    double low, 
                    double close, 
                    double last, 
                    double volume, 
                    double size, 
                    double bid, 
                    double ask, 
                    double bidsize, 
                    double asksize, 
                    double open_int, 
                    double go_buy, 
                    double go_sell, 
                    double go_base, 
                    double go_base_backed, 
                    double high_limit, 
                    double low_limit, 
                    int trading_status, 
                    double volat, 
                    double theor_price
            )
            {
                if (LastQuote == null || (LastQuote != null && LastQuote.Code != symbol))  /* updated symbol has never been saved so far */
                {
                    if (symbol == "RTS-6.13_FT" || symbol == "RTSo-6.13_FT" || symbol == "RTSc-6.13_FT")
                    {
                        LastQuote = new Quote(symbol, datetime, last, volume, trading_status, UpDateQuote);
                        tw = File.AppendText(symbol + ".txt");
                        Console.WriteLine("\n[SmartServer_UpdateQuote message]: quote created for " + symbol);
                        tw.WriteLine("\n[SmartServer_UpdateQuote message]: quote created for " + symbol);
                        Console.WriteLine("[Symbol = " + symbol + "]\n[DateTime = " + datetime.ToShortDateString() + "]\n[Last = " + last + "]\n[Volume = " + volume + "]\n[TradingStatus = " + trading_status + "]");
                        tw.WriteLine("[Symbol = " + symbol + "]\n[DateTime = " + datetime.ToShortDateString() + "]\n[Last = " + last + "]\n[Volume = " + volume + "]\n[TradingStatus = " + trading_status + "]");
                        tw.Close();
                    }
                }
                else
                    LastQuote.UpDate(trading_status); // the last saved quote has been updated now
            }                                         // GUI labels corresponding to instrument's quote are updated by separate thread     

            public void smartServer_AddTick(
                    string symbol, 
                    System.DateTime datetime, 
                    double price, 
                    double volume, 
                    string tradeno, 
                    StClientLib.StOrder_Action action
            )
            {
                if (LastQuote != null) // update is shown ONLY if 'symbol' corresponds to last quote symbol
                {
                    DateTime now1 = DateTime.Now;
    
                    long LastNo = 0;
                    long.TryParse(tradeno, out LastNo);
    
                    if (LastQuote.Code == symbol)
                    {
                        LastQuote.UpDate(datetime, price, volume, LastNo, action);
    
    
                        if (now1.Minute > datetime.Minute)   //user machine clock runs faster than server clock.
                                                             //in this case it is critically important to adjust bar fetch times
                                                             //i.e. fetch bars a bit later than .00.00
                        {
                            userMachineFaster = 1;
    
                            if (indicate == 0)
                                Console.Write("\nLocal time exceeds Server time by ");
    
                            if (now1.Second >= datetime.Second)
                            {
                                secDifference = now1.Second - datetime.Second;
                                minDifference = now1.Minute - datetime.Minute;
                            }
    
                            else
                            {
                                secDifference = now1.Second + (60 - datetime.Second);
                                minDifference = now1.Minute - datetime.Minute - 1;
                            }
                        } 
    
                        else if (now1.Minute < datetime.Minute) //local minute is less than server minute                                               
                        {
                            userMachineFaster = 0;
    
                            if (indicate == 0)
                                Console.Write("\nServer time exceeds Local time by ");

                            if (datetime.Second >= now1.Second)
                            {
                                secDifference = datetime.Second - now1.Second;
                                minDifference = datetime.Minute - now1.Minute;
                            }

                            else
                            {
                                secDifference = (60 - now1.Second) + datetime.Second;
                                minDifference = datetime.Minute - now1.Minute - 1;
                            }
                        }              
    
                        else //minutes are equal
                        {
                            minDifference = 0;
    
                            if (now1.Second >= datetime.Second)
                            {
                                userMachineFaster = 1;
    
                                if (indicate == 0)
                                    Console.Write("\nLocal time exceeds Server time by ");
    
                                secDifference = now1.Second - datetime.Second;
                            }
    
                            else
                            {
                                userMachineFaster = 0;
    
                                if (indicate == 0)
                                    Console.Write("\nServer time exceeds Local time by ");
    
                                secDifference = datetime.Second - now1.Second;
                            }
                        }
    
                        if (indicate == 0)
                            Console.Write(minDifference + " minutes " + secDifference + " seconds\n");
    
                        indicate = 1;
    
                        tw = File.AppendText(symbol + ".txt");
                        
                        Console.WriteLine("\n[SmartServer_AddTick message]: Tick updated for " + symbol);
                        tw.WriteLine("\n[SmartServer_AddTick message]: Tick updated for " + symbol);
                        Console.WriteLine("[Symbol = " + symbol + "]\n[ServerTime = " + datetime.ToLongTimeString() + "]\n[LocalTime = " + DateTime.Now.ToLongTimeString() + "]\n[Price = " + price + "]\n[Volume = " + volume + "]\n[TradeNo = " + tradeno + "]\n");
                        tw.WriteLine("[Symbol = " + symbol + "]\n[ServerTime = " + datetime.ToLongTimeString() + "]\n[LocalTime = " + DateTime.Now.ToLongTimeString() + "]\n[Price = " + price + "]\n[Volume = " + volume + "]\n[TradeNo = " + tradeno + "]\n");
                        tw.Close();
    
                    }
                }
            }
    
            public void smartServer_UpdateBidAsk(
                    string symbol, 
                    int row, 
                    int nrows, 
                    double bid, 
                    double bidsize, 
                    double ask, 
                    double asksize
            )
            {
                if (row == 0 && LastQuote != null) // update is detected ONLY if 'symbol' corresponds to last quote symbol
                {
                    if (LastQuote.Code == symbol)
                    {
                        LastQuote.UpDate(ask, asksize, bid, bidsize);
                        tw = File.AppendText(symbol + ".txt");
                        
                        Console.WriteLine("\n[SmartServer_UpdateBidAsk message]: Bid/Ask updated for " + symbol);
                        tw.WriteLine("\n[SmartServer_UpdateBidAsk message]: Bid/Ask updated for " + symbol);
                        Console.WriteLine("[Symbol = " + symbol + "]\n[Bid = " + bid + "]\n[BidSize = " + bidsize + "]\n[Ask = " + ask + "]\n[AskSize = " + asksize + "]\n");
                        tw.WriteLine("[Symbol = " + symbol + "]\n[Bid = " + bid + "]\n[BidSize = " + bidsize + "]\n[Ask = " + ask + "]\n[AskSize = " + asksize + "]\n");
                        tw.Close();
                    }
                }
            }

            public void smartServer_AddBar(
                    int row, 
                    int nrows, 
                    string symbol, 
                    StClientLib.StBarInterval interval, 
                    System.DateTime datetime, 
                    double open, 
                    double high, 
                    double low, 
                    double close, 
                    double volume, 
                    double open_int
            )
            {
                if (datetime > ToDateTimePicker.Value) // add new bar into list
                {                
    
                    sw1.Start();
        
                    InfoBars.Add(new Bar(symbol, datetime, open, high, low, close, volume)); //datetime is system time (Baku on developer system, Moscow on customer system)                                     
        
                    tw = File.AppendText(symbol + ".txt");
        
                    Console.WriteLine(InfoBars.Last().ToString());
                    tw.WriteLine(InfoBars.Last().ToString());
        
                    if (currBar == 1) //current bar, need to assign o0
                    {
                        Console.WriteLine("This was Current Bar");
        
                        o0_value = open;
        
                        currBar = 0;
        
                        sw1.Stop();
                        Console.WriteLine("o2 received in " + sw1.ElapsedMilliseconds + " msec");
        
                    }
                    
                    else if (currBar == 0) //last bar, need to assign o1 and c1
                    {
                        Console.WriteLine("This was Last Bar");
        
                        o1_value = open;
                        c1_value = close;
        
                        currBar = 1;
        
                        sw1.Stop();
                        Console.WriteLine("o1 and c1 received in " + sw1.ElapsedMilliseconds + " msec");
                        sw1.Start();
        
                        // and the algorithm may be implemented over here 
                        // because there is no event to be received
                        //
                        // buy and sell cases are disjoint i.e. cannot occur simultaneously, so 
                        // for any given bar we will have either sell or buy
        
                        if (o1_value - c1_value > -1000 && c1_value - o0_value >= 20)
                        {
                            //open buy order and close at end of current bar                                             
        
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
        
                            buyDone = 1;
                        }
        
                        else if (c1_value - o1_value > -1000 && o0_value - c1_value >= 20)
                        {
                            //open sell order and close at end of current bar
        
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
        
                            sellDone = 1;
                        }
        
                        else
                        {
                            Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] NO CONDITION HAS BEEN SATISFIED. NO BUY/SELL PERFORMED");
                            tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "] NO CONDITION HAS BEEN SATISFIED. NO BUY/SELL PERFORMED");
                        }
        
                        sw1.Stop();
                        Console.WriteLine("Decision done in " + sw1.ElapsedMilliseconds + " msec");
                        sw1.Reset();
        
                    }
        
                    tw.Close();
                    
                    if (closeFlag == 1) //add this bar to close value checkouts
                    {   
                        twclose.WriteLine("Close Value (C0): " + close); 
                        closeReady = 1; 
                    }
        
                    if (openFlag == 1) //add this bar to open value checkouts
                    {   
                        twopen.WriteLine("Open Value (O0): " + open);    
                        openReady = 1; 
                    }
                }
    
                if (row == nrows - 1) // last bar in response
                {
                    if (InfoBars.Count == 0 || datetime > ToDateTimePicker.Value) // last bar's time is ahead of chosen bar's time
                        try
                        {
                            DateTime dtFrom = (InfoBars.Count == 0 ? DateTime.Now : InfoBars.Last().Clock.AddMinutes(-60));
                            Writers.WriteLine("Energy", "log", "{0} GetBars {1}:{2} From {3}", DateTime.Now, SymbolTextBox.Text, GetInterval, dtFrom);
                            SmartServer.GetBars(SymbolTextBox.Text, GetInterval, dtFrom, 500);
                        }
                        catch (Exception Error)
                        {
                            Writers.WriteLine("Energy", "log", "{0} Error on GetBars {1}", DateTime.Now, Error.Message);
                        }
                    else
                        new Thread(ThreadBarsSave).Start(); // otherwise all bars were received so we can dump them
                }    
            }

            public void smartServer_AddPortfolio(
                    int row, 
                    int nrows, 
                    string portfolioName, 
                    string portfolioExch, 
                    StClientLib.StPortfolioStatus portfolioStatus
            )
            {
                tw = File.AppendText("RTS-6.13_FT.txt");
                Console.WriteLine("\nPortfolio => portfolioName:" + portfolioName + " portfolioExch:" + portfolioExch + " StPortfolioStatus_Broker:" + (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker));
                tw.WriteLine("\nPortfolio => portfolioName:" + portfolioName + " portfolioExch:" + portfolioExch + " StPortfolioStatus_Broker:" + (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker));
                tw.Close();
    
                if (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker)
                {
                    Console.WriteLine("\nPortfolio => portfolioName:" + portfolioName + " portfolioExch:" + portfolioExch + " StPortfolioStatus_Broker:" + (portfolioStatus == StPortfolioStatus.StPortfolioStatus_Broker));
    
                    if (InfoPortfolios.IndexOf(portfolioName) == -1)
                    {
                        InfoPortfolios.Add(portfolioName);
                        if (PortfoliosComboBox.SelectedIndex == -1 && PortfoliosComboBox.Items.Count > 0)
                            PortfoliosComboBox.SelectedIndex = 0;
                    }
                    try
                    {
                        SmartServer.ListenPortfolio(portfolioName);
                    }
                    catch (Exception Error)
                    {
                        Console.WriteLine("Error occured in ListenPortfolio");
                    }
                    
                    try
                    {
                        SmartServer.GetMyClosePos(portfolioName);
                    }
                    catch (Exception Error)
                    {
                        Writers.WriteLine("Energy", "log", "{0} Error in GetMyClosePos {1}, {2}", DateTime.Now, portfolioName, Error.Message);
                    }
                    
                    try
                    {
                        SmartServer.GetMyOrders(0, portfolioName);
                    }
                    catch (Exception Error)
                    {
                        Writers.WriteLine("Energy", "log", "{0} Error in GetMyOrders {1}, {2}", DateTime.Now, portfolioName, Error.Message);
                    }
                    
                    try
                    {
                        SmartServer.GetMyTrades(portfolioName);
                    }
                    catch (Exception Error)
                    {
                        Writers.WriteLine("Enegy", "log", "{0} Error in GetMyTrades {1}, {2}", DateTime.Now, portfolioName, Error.Message);
                    }
                }
            }
    
            public void smartServer_SetPortfolio(
                    string portfolio, 
                    double cash, 
                    double leverage, 
                    double comission, 
                    double saldo
            )
            {
                tw = File.AppendText("RTS-6.13_FT.txt");
    
                Console.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]" + " Portfolio:" + portfolio + " Cash:" + cash + " Comission:" + comission + " Saldo:" + saldo + " Leverage:" + leverage);
                tw.WriteLine("\n[" + DateTime.Now.ToLongTimeString() + "]" + " Portfolio:" + portfolio + " Cash:" + cash + " Comission:" + comission + " Saldo:" + saldo + " Leverage:" + leverage);
                tw.Close();
            }
    
            public void smartServer_OrderSucceeded(
                    int cookie, 
                    string orderid
            )
            {
                Console.WriteLine("\nOrder succeeded. Order cookie: " + cookie + " Order ID: " + orderid);
            }
            
            public void smartServer_OrderFailed(
                    int cookie, 
                    string orderid, 
                    string reason
            )
            {
                Console.WriteLine("\nOrder failed. Order cookie: " + cookie + " Order ID: " + orderid + "Reason: " + reason);
            }
            
            public void UpDate(double Ask, double AskVolume, double Bid, double BidVolume)                                     /* used by smartServer_UpdateBidAsk listener */
            {
                InfoAsk = Ask;
                InfoBid = Bid;
                InfoAskVolume = (int)AskVolume;
                InfoBidVolume = (int)BidVolume;
                new Thread(ThreadUpdate).Start();
            }
            public void UpDate(DateTime Clock, double Price, double Volume, long LastNo, StClientLib.StOrder_Action Action)    /* used by smartServer_AddTick listener     */
            {
                InfoLastNo = LastNo;
                InfoLastClock = Clock;
                InfoLastPrice = Price;
                InfoLastVolume = (int)Volume;
                InfoLastAction = Action;
                new Thread(ThreadUpdate).Start();
            }        
    }
}