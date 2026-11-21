using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        CalendarView calendar;
        Label calTitle, calStats;

        static readonly string[] Months = { "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень", "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень" };
        static readonly Color[] ChannelColors =
        {
            Color.FromArgb(190, 242, 100), Color.FromArgb(96, 165, 250), Color.FromArgb(244, 114, 182),
            Color.FromArgb(251, 191, 36), Color.FromArgb(167, 139, 250), Color.FromArgb(45, 212, 191)
        };

        Color ChannelColor(string id)
        {
            int i = settings.Channels.FindIndex(c => c.Id == id);
            return ChannelColors[(i < 0 ? 0 : i) % ChannelColors.Length];
        }

        Page BuildCalendarPage()
        {
            calendar = new CalendarView { Dock = DockStyle.Fill };
            calendar.EntriesFor = day => settings.Published
                .Where(p => BestTime.ToKyiv(p.PublishAtUtc).Date == day)
                .OrderBy(p => p.PublishAtUtc)
                .Select(p => new CalendarView.Entry
                {
                    Time = BestTime.ToKyiv(p.PublishAtUtc).ToString("HH:mm"),
                    Title = p.Title,
                    Color = ChannelColor(p.ChannelId),
                    Tag = p.VideoId
                }).ToList();
            calendar.EntryClicked += en => OpenUrl("https://youtube.com/shorts/" + en.Tag);
            calTitle = Section2("");
            calStats = new Label { AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(14, 3, 3, 3) };
            var bar = Header2(Row(calTitle, calStats),
                Btn("", (s, e) => MoveMonth(-1), ButtonKind.Secondary, Glyphs.Prev),
                Btn("Сьогодні", (s, e) => { calendar.Month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); RefreshCalendar(); }, ButtonKind.Secondary),
                Btn("", (s, e) => MoveMonth(1), ButtonKind.Secondary, Glyphs.Next));
            return new Page("Календар", "Коли виходять ваші шортси — за часом Києва. Натисніть на шортс, щоб відкрити його на YouTube", Stack(bar, 0, calendar, 100));
        }

        void MoveMonth(int d)
        {
            calendar.Month = calendar.Month.AddMonths(d);
            RefreshCalendar();
        }

        void RefreshCalendar()
        {
            if (calendar == null) return;
            calendar.Today = BestTime.ToKyiv(DateTime.UtcNow).Date;
            calTitle.Text = Months[calendar.Month.Month - 1] + " " + calendar.Month.Year;
            var inMonth = settings.Published.Where(p => { var k = BestTime.ToKyiv(p.PublishAtUtc); return k.Year == calendar.Month.Year && k.Month == calendar.Month.Month; }).ToList();
            int upcoming = settings.Published.Count(p => p.PublishAtUtc > DateTime.UtcNow);
            calStats.Text = inMonth.Count + " шортсів цього місяця" + (upcoming > 0 ? " · заплановано наперед: " + upcoming : "");
            calendar.Invalidate();
        }
    }
}
