using System;
using System.Globalization;

namespace LexisDesk {
    // FSRS-6 memory equations and defaults, ported from open-spaced-repetition/go-fsrs
    // commit 61159b9b3891b1279b63206afef4086d7ef92992 (MIT). See licenses/FSRS-MIT.txt.
    // Learning steps and early-practice protection below are world speace policies.
    public static class ReviewScheduler {
        public const int Version=1;
        public const double TargetRetention=.9;
        public const int MaximumInterval=365;
        static readonly double[] W={.212,1.2931,2.3065,8.2956,6.4133,.8334,3.0194,.001,1.8722,.1666,.796,1.4835,.0614,.2629,1.6483,.6014,1.8729,.5425,.0912,.0658,.1542};
        static readonly int[] LegacyDays={1,1,3,7,14,30,60};
        static double Clamp(double v,double lo,double hi){return Math.Max(lo,Math.Min(hi,v));}
        static double InitialDifficulty(int rating){return W[4]-Math.Exp(W[5]*(rating-1))+1;}
        public static DateTime Parse(string value){var time=DateTime.ParseExact(value,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);return time.Kind==DateTimeKind.Utc?time.ToLocalTime():time;}
        public static double Elapsed(DateTime last,DateTime now){return Math.Max(0,(now.ToUniversalTime()-last.ToUniversalTime()).TotalDays);}
        static DateTime AfterDays(DateTime now,double days){return now.ToUniversalTime().AddDays(days).ToLocalTime();}
        public static double Recall(double stability,double days){return Math.Pow(1+(Math.Pow(.9,-1/W[20])-1)*Math.Max(0,days)/Clamp(stability,.001,36500),-W[20]);}
        public static void UpdateMemory(CardState card,int rating,double elapsedDays) {
            if(rating<1||rating>4)throw new ArgumentOutOfRangeException("rating");
            double s=card.Stability,d=card.Difficulty;
            if(s<=0){s=W[rating-1];d=Clamp(InitialDifficulty(rating),1,10);}
            else {
                double nextD=Clamp(W[7]*InitialDifficulty(4)+(1-W[7])*(d+(10-d)*(-W[6]*(rating-3))/9),1,10);
                if(elapsedDays<1) {
                    double growth=Math.Exp(W[17]*(rating-3+W[18]))*Math.Pow(s,-W[19]);
                    if(rating>=2)growth=Math.Max(1,growth);
                    s*=growth;
                } else {
                    double recall=Recall(s,Math.Floor(elapsedDays));
                    if(rating==1)s=Math.Min(W[11]*Math.Pow(d,-W[12])*(Math.Pow(s+1,W[13])-1)*Math.Exp((1-recall)*W[14]),s/Math.Exp(W[17]*W[18]));
                    else s*=1+Math.Exp(W[8])*(11-d)*Math.Pow(s,-W[9])*(Math.Exp((1-recall)*W[10])-1)*(rating==2?W[15]:1)*(rating==4?W[16]:1);
                }
                d=nextD;
            }
            card.Stability=Clamp(s,.001,36500);card.Difficulty=d;
        }
        public static int Interval(double stability){return (int)Clamp(Math.Round(stability,MidpointRounding.AwayFromZero),1,MaximumInterval);}
        public static string StageName(CardState card){return card==null?"尚未学习":card.SchedulerVersion==0?"旧版计划":card.Stage=="learning"?"初学巩固":card.Stage=="relearning"?"遗忘后重学":"间隔复习";}
        public static string Description(CardState card) {
            if(card==null)return "首次学习后先安排 10 分钟巩固，再根据回忆表现安排后续复习。";
            return StageName(card)+" · 下次 "+Parse(card.Due).ToString("yyyy年M月d日 HH:mm")+"\n"+(card.SchedulerVersion==0?"保留旧版日期，到期作答后衔接新计划。":card.ScheduleReason);
        }
        static void AdoptLegacy(CardState card) {
            double interval=card.Level==0?10.0/1440:LegacyDays[card.Level];
            card.SchedulerVersion=Version;card.Stability=Math.Max(.1,interval);card.Difficulty=Clamp(InitialDifficulty(3),1,10);
            card.Stage=card.Level==0?"relearning":"review";card.IntervalDays=interval;
            // Legacy logs have dates only. This is an explicitly approximate seed,
            // not a replay of invented precise historical review timestamps.
            card.LastReview=AfterDays(Parse(card.Due),-interval).ToString("o");
        }
        public static void Schedule(CardState card,int grade,DateTime now,bool isNew) {
            bool legacy=!isNew&&card.SchedulerVersion==0;
            if(legacy)AdoptLegacy(card);
            string previousStage=card.Stage;
            double elapsed=isNew?0:Elapsed(Parse(card.LastReview),now);
            UpdateMemory(card,grade+1,elapsed);
            card.SchedulerVersion=Version;card.LastReview=now.ToString("o");
            if(grade==0)card.Lapses++;
            if(isNew||grade==0||((previousStage=="learning"||previousStage=="relearning")&&grade==1)) {
                card.Stage=isNew?"learning":previousStage=="learning"?"learning":"relearning";
                card.Level=0;card.IntervalDays=10.0/1440;card.Due=AfterDays(now,10.0/1440).ToString("o");
                card.ScheduleReason=isNew?"初次接触：10 分钟后检验回忆。":grade==0?"未能独立想起：10 分钟后重新检验。":"短时巩固仍吃力：10 分钟后再检验。";
            } else {
                int days=Interval(card.Stability);card.IntervalDays=days;card.Stage="review";card.Due=AfterDays(now,days).ToString("o");
                card.Level=days>=60?6:days>=30?5:days>=14?4:days>=7?3:days>=3?2:1;
                card.ScheduleReason=(grade==1?"想起但吃力":"独立想起")+"：根据本词回忆记录安排 "+days+" 天后复习。";
            }
            if(legacy)card.ScheduleReason+=" 旧记录以原间隔近似衔接。";
        }
        public static bool Valid(CardState c) {
            if(c.SchedulerVersion==0)return true;
            DateTime last;
            return c.SchedulerVersion==Version&&(c.Stage=="learning"||c.Stage=="relearning"||c.Stage=="review")&&
                !double.IsNaN(c.Stability)&&c.Stability>=.001&&c.Stability<=36500&&
                !double.IsNaN(c.Difficulty)&&c.Difficulty>=1&&c.Difficulty<=10&&
                !double.IsNaN(c.IntervalDays)&&c.IntervalDays>0&&c.IntervalDays<=MaximumInterval&&
                DateTime.TryParseExact(c.LastReview,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out last);
        }
    }
}
