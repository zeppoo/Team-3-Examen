[System.Serializable]
public class ScoreData
{
    public int score;
    public int goodPoint;
    public int failPoints;

    public ScoreData(int newScore)
    {
        score = newScore;
    }

    public void ApplyHit(bool wasGoodHit)
    {
        if (wasGoodHit)
            score += goodPoint;
        else
            score -= failPoints;
    }
}
