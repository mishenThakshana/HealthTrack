namespace HealthTrack.Helpers;

public static class Bmi
{
    public static string Category(decimal bmi) => bmi switch
    {
        < 18.5m => "Underweight",
        < 25m => "Healthy weight",
        < 30m => "Overweight",
        _ => "Obese"
    };
}
