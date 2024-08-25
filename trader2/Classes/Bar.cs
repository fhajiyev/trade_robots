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
    public class Bar
    {
        public enum Type { Open, Max, Min, Close, AVG }

        private string InfoCode;
        private System.DateTime InfoClock;
        private double InfoOpen;
        private double InfoHigh;
        private double InfoLow;
        private double InfoClose;
        private double InfoVolume;

        public Bar(
                string code,
                System.DateTime clock,
                double open,
                double high,
                double low,
                double close,
                double volume
        )
        {
            InfoCode = code;
            InfoClock = clock;
            InfoOpen = open;
            InfoHigh = high;
            InfoLow = low;
            InfoClose = close;
            InfoVolume = volume;
        }

        public string Code { get { return InfoCode; } }
        public DateTime Clock { get { return InfoClock; } }
        public double Open { get { return InfoOpen; } }
        public double High { get { return InfoHigh; } }
        public double Low { get { return InfoLow; } }
        public double Close { get { return InfoClose; } }
        public double Volume { get { return InfoVolume; } }
        public double GetBy(Type type)
        {
            return (type == Type.Open ?
            InfoOpen : type == Type.Max ?
            InfoHigh : type == Type.Min ?
            InfoLow : type == Type.Close ?
            InfoClose : type == Type.AVG ?
            (InfoLow + InfoHigh) / 2.0d : 0.0d);
        }

        public override string ToString()
        {
            return "\nBar[" + InfoCode + "] " + InfoClock.ToString() +
            " Open:" + InfoOpen +
            " High:" + InfoHigh +
            " Low:" + InfoLow +
            " Close:" + InfoClose +
            " Volume:" + InfoVolume;
        }
    }
}