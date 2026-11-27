using System;
using System.Collections.Generic;
using System.Globalization;

namespace DepartmentModule.Helpers
{
    public static class TenderDateRules
    {
        private static readonly TimeZoneInfo IndiaTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

        public static bool TryParseIndiaLocalDate(string value, out DateTime utc, bool useEndOfDay = false)
        {
            utc = DateTime.MinValue;
            DateTime localDate;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out localDate))
                return false;
            DateTime localDateTime = useEndOfDay ? localDate.Date.AddDays(1).AddTicks(-1) : localDate.Date;
            utc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), IndiaTimeZone);
            return true;
        }

        public static bool TryParseIndiaLocalDateTime(string value, out DateTime utc)
        {
            utc = DateTime.MinValue;
            DateTime localDateTime;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out localDateTime))
                return false;
            utc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), IndiaTimeZone);
            return true;
        }

        public static DateTime GetIndiaCalendarDate(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                IndiaTimeZone).Date;
        }

        public static string ValidateTenderSchedule(DateTime? publication, DateTime? submissionStart,
            DateTime? submissionClosing, DateTime? technicalOpening, DateTime? financialOpening,
            DateTime? preBidMeeting, DateTime utcNow, bool isNewTender)
        {
            List<string> errors = new List<string>();
            if (isNewTender && publication.HasValue)
            {
                DateTime indiaToday = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), IndiaTimeZone).Date;
                DateTime indiaTodayUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(indiaToday, DateTimeKind.Unspecified), IndiaTimeZone);
                if (publication.Value < indiaTodayUtc)
                    errors.Add("Set the publication date to today or a future date (IST).");
            }
            if (submissionStart.HasValue != submissionClosing.HasValue)
                errors.Add("Enter both the submission start date and submission closing date.");
            if (publication.HasValue && submissionStart.HasValue &&
                publication.Value >= submissionStart.Value)
                errors.Add("Set the publication date before the submission start date.");
            if (submissionStart.HasValue && submissionClosing.HasValue &&
                GetIndiaCalendarDate(submissionStart.Value) >= GetIndiaCalendarDate(submissionClosing.Value))
                errors.Add("Set the submission closing date later than the submission start date.");
            if (technicalOpening.HasValue &&
                (!submissionClosing.HasValue || technicalOpening.Value <= submissionClosing.Value))
                errors.Add("Set the technical bid opening date after the submission closing date.");
            if (financialOpening.HasValue &&
                (!technicalOpening.HasValue || financialOpening.Value <= technicalOpening.Value))
                errors.Add("Set the financial bid opening date after the technical bid opening date.");
            if (preBidMeeting.HasValue && submissionClosing.HasValue &&
                GetIndiaCalendarDate(preBidMeeting.Value) >= GetIndiaCalendarDate(submissionClosing.Value))
                errors.Add("Set the pre-bid meeting date before the submission closing date.");
            if (preBidMeeting.HasValue && publication.HasValue &&
                preBidMeeting.Value < publication.Value)
                errors.Add("Set the pre-bid meeting date on or after the publication date.");
            return errors.Count == 0 ? null : String.Join(" ", errors);
        }
    }
}
