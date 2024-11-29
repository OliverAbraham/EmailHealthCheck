namespace EmailHealthCheck;

public class Rating
{
    public int AgeDays { get; set; }
    public string Result { get; set; }

    public override string ToString()
    {
        return $"        age <= {AgeDays,9} days --> \"{Result}\"\n";
    }

    public static string GetRatingForAge(List<Rating> ratings, double ageInDays)
    {
        int age = (int)ageInDays;

        foreach(var rating in ratings)
        {
            if ((int)age <= rating.AgeDays)
                return rating.Result;
        }

        return age.ToString();
    }
}
