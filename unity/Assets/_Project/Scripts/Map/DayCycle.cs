using System;
using System.Globalization;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 실제 한국 시간의 낮 · 밤. 서울 하늘의 해 고도를 계산해서 Daylight (0 = 밤, 1 = 낮) 를 준다 —
    /// 계절마다 해 뜨고 지는 시각이 바뀌고, 해가 지평선 ±6° 일 때(어스름) 부드럽게 넘어간다.
    /// 클라이언트 시계(UTC) 기준이라 서버 · 네트워크가 필요 없다. MapLighting 이 맵의 lighting(낮) 과 night 를 섞는 데 쓴다.
    /// 시험용으로 시각 고정: 실행 인자 `-hour 21.5` (한국 시각) 또는 OverrideHour.
    /// </summary>
    public static class DayCycle
    {
        private const double Latitude = 37.57; // 서울
        private const double Longitude = 126.98;
        private const double KoreaUtcOffset = 9.0;
        private const float TwilightDegrees = 6f; // 시민박명: 해가 지평선 아래 6° 면 완전히 밤

        /// <summary>한국 시각(0..24)으로 고정. NaN = 실제 시각</summary>
        public static float OverrideHour { get; set; } = float.NaN;

        // Domain Reload 가 꺼져 있어 static 값이 Play 사이에 남는다 → Play 마다 실행 인자로 다시
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => OverrideHour = HourFromCommandLine();

        /// <summary>0 = 밤, 1 = 낮, 사이 = 해 뜨고 질 무렵</summary>
        public static float Daylight => Smooth(Mathf.InverseLerp(-TwilightDegrees, TwilightDegrees, (float)SunElevation(Now())));

        /// <summary>
        /// 저녁노을 세기 0..1: 해가 지는 쪽(오후)일 때만, 고도 12° 부터 차올라 3° ~ -1° 에서 가장 진하고 -6° 면 없다.
        /// MapLighting 이 낮 · 밤을 섞은 값 위에 맵의 dusk 를 이만큼 섞는다
        /// </summary>
        public static float Dusk
        {
            get
            {
                DateTime utc = Now();
                double elevation = SunElevation(utc);
                if (SunElevation(utc.AddMinutes(10)) >= elevation)
                {
                    return 0f; // 해가 뜨는 중 (오전)
                }
                float e = (float)elevation;
                return Smooth(Mathf.InverseLerp(-TwilightDegrees, -1f, e)) * Smooth(Mathf.InverseLerp(12f, 3f, e));
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // 지금 (UTC). OverrideHour 가 있으면 오늘 날짜(한국)의 그 시각
        private static DateTime Now()
        {
            DateTime utc = DateTime.UtcNow;
            if (float.IsNaN(OverrideHour))
            {
                return utc;
            }
            DateTime korea = utc.AddHours(KoreaUtcOffset).Date.AddHours(OverrideHour);
            return korea.AddHours(-KoreaUtcOffset);
        }

        /// <summary>서울에서 본 해의 고도 (도). 간단한 천문 근사식 — 몇 분 오차는 상관없다</summary>
        public static double SunElevation(DateTime utc)
        {
            const double deg = Math.PI / 180.0;
            int day = utc.DayOfYear;
            double declination = 23.44 * Math.Sin(2.0 * Math.PI * (284 + day) / 365.0);
            double b = 2.0 * Math.PI * (day - 81) / 365.0;
            double equationOfTime = 9.87 * Math.Sin(2 * b) - 7.53 * Math.Cos(b) - 1.5 * Math.Sin(b); // 분
            double solarHours = utc.TimeOfDay.TotalHours + Longitude / 15.0 + equationOfTime / 60.0;
            double hourAngle = 15.0 * (solarHours - 12.0);
            double sin = Math.Sin(Latitude * deg) * Math.Sin(declination * deg)
                + Math.Cos(Latitude * deg) * Math.Cos(declination * deg) * Math.Cos(hourAngle * deg);
            return Math.Asin(Math.Clamp(sin, -1.0, 1.0)) / deg;
        }

        private static float HourFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-hour" && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float hour))
                {
                    return Mathf.Repeat(hour, 24f);
                }
            }
            return float.NaN;
        }
    }
}
