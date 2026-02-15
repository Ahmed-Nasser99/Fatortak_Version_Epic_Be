using fatortak.Entities;

namespace fatortak.Helpers
{
    public static class GeneralVacationSeeder
    {
        public static List<GeneralVacation> GetEgyptianHolidays(int year, Guid tenantId)
        {
            return new List<GeneralVacation>
        {
            new GeneralVacation { TenantId = tenantId, Name = "عيد الميلاد المجيد", Date = new DateOnly(year, 1, 7), DaysOfVacation = 1 },
            new GeneralVacation { TenantId = tenantId, Name = "عيد الشرطة / ثورة يناير", Date = new DateOnly(year, 1, 25), DaysOfVacation = 1 },
            new GeneralVacation { TenantId = tenantId, Name = "عيد تحرير سيناء", Date = new DateOnly(year, 4, 25), DaysOfVacation = 1 },
            new GeneralVacation { TenantId = tenantId, Name = "عيد العمال", Date = new DateOnly(year, 5, 1), DaysOfVacation = 1 },
            new GeneralVacation { TenantId = tenantId, Name = "ثورة 23 يوليو", Date = new DateOnly(year, 7, 23), DaysOfVacation = 1 },
            new GeneralVacation { TenantId = tenantId, Name = "عيد القوات المسلحة", Date = new DateOnly(year, 10, 6), DaysOfVacation = 1 }
        };
        }
    }

}
