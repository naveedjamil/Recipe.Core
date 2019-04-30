using System;

namespace Recipe.NetCore.Model
{
    public class DateTimeIntervalModel
    {
        public string Interval { get; set; }
        public DateTime ToDateTime { get; set; }
        public DateTime FromDateTime { get; set; }
    }
}
