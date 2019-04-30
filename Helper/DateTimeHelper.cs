using System;
using System.Collections.Generic;
using System.Linq;
using Recipe.NetCore.Model;

namespace Recipe.NetCore.Helper
{
    public static class DateTimeHelper
    {
        public static IEnumerable<int> GetYearsOrMonthsBetweenDates(DateTime start, DateTime end)
        {
            var list = new List<int>();

            for (var d = start; d <= end; d = d.AddMonths(1))
            { list.Add(Convert.ToInt32(d.ToString("yyyy"))); }

            list = list.Distinct().ToList();

            return list;
        }

        public static int GetWeekNumber(DateTime date)
        {
            var dt = date;
            var weekOfMonth = (dt.Day + ((int)dt.DayOfWeek)) / 7 + 1;
            return weekOfMonth;
        }

        public static IEnumerable<DateTimeIntervalModel> SplitDateRangeIntoHoursIntervals(DateTime start, DateTime end, int parts)
        {
            var StartOfFromDateRange = start;
            var bigInterval = end - start;
            var returnValue = new List<DateTimeIntervalModel>();

            var smallInterval = new TimeSpan(bigInterval.Ticks / parts);

            while (StartOfFromDateRange < end)
            {
                var currentFromDate = StartOfFromDateRange;
                var currentToDate = StartOfFromDateRange + smallInterval;

                StartOfFromDateRange = StartOfFromDateRange + smallInterval;

                returnValue.Add(new DateTimeIntervalModel()
                {
                    Interval = $"{currentFromDate.ToString("hh tt")}-{currentToDate.ToString("hh tt")}",
                    FromDateTime = currentFromDate,
                    ToDateTime = currentToDate
                });
            }

            return returnValue;
        }

        public static IEnumerable<DateTimeIntervalModel> SplitDateRangeIntoMinutesIntervals(DateTime start, DateTime end, int parts)
        {
            var returnValue = new List<DateTimeIntervalModel>();

            var smallInterval = new TimeSpan(TimeSpan.TicksPerMinute * parts);

            end = end.RoundToNearest(smallInterval);
            var StartOfFromDateRange = start.RoundToNearest(smallInterval);

            while (StartOfFromDateRange < end)
            {
                var currentFromDate = StartOfFromDateRange;
                var currentToDate = StartOfFromDateRange + smallInterval;

                StartOfFromDateRange = StartOfFromDateRange + smallInterval;

                returnValue.Add(new DateTimeIntervalModel()
                {
                    Interval = $"{currentFromDate.ToString("hh:mm tt")}-{currentToDate.ToString("hh:mm tt")}",
                    FromDateTime = currentFromDate,
                    ToDateTime = currentToDate
                });
            }

            return returnValue;
        }

        public static DateTime RoundToNearest(this DateTime dt, TimeSpan d)
        {
            var delta = dt.Ticks % d.Ticks;
            bool roundUp = delta > d.Ticks / 2;
            var offset = roundUp ? d.Ticks : 0;

            return new DateTime(dt.Ticks + offset - delta, dt.Kind);
        }

        public static IEnumerable<DateTimeIntervalModel> SplitDateRangeByDayChunk(DateTime start, DateTime end, int dayChunkSize)
        {
            DateTime chunkEnd;
            while ((chunkEnd = start.AddDays(dayChunkSize)) < end)
            {
                yield return new DateTimeIntervalModel() { FromDateTime = start, ToDateTime = chunkEnd };
                start = chunkEnd;
            }
            yield return new DateTimeIntervalModel() { FromDateTime = start, ToDateTime = end };
        }
    }
}
