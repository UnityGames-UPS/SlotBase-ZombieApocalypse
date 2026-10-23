using System.Collections.Generic;
using System;

[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace;
}

[Serializable]
public class MessageData
{
    public string type;
    public Data payload = new();
}

[Serializable]
public class Data
{
    public int betIndex;
}

[Serializable]
public class JackpotOpenRequest
{
    public string type = "JACKPOT_OPEN";
    public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
    public string tier;
}

// InIt Data Classes

[Serializable]
public class Values
{
    public string miniJackpot { get; set; }
    public string minorJackpot { get; set; }
    public string majorJackpot { get; set; }
    public string grandJackpot { get; set; }
}


[Serializable]
public class JackpotData
{
    public Values values { get; set; }
}

[Serializable]
public class BalanceSyncPayload
{
    public double balance;
}

// Root Class

[Serializable]
public class Root
{
    //init Data
    public string id { get; set; }
    public GameData gameData { get; set; }
    public Features features { get; set; }
    public UiData uiData { get; set; }
    public JackpotData jackpotData { get; set; }
    public Values values { get; set; }

    // Common Data
    public Player player { get; set; }

    // Result Data
    public bool success { get; set; }
    public List<List<string>> matrix { get; set; }
    public Payload payload { get; set; }

}

// Init Data Classes

[Serializable]
public class GameData
{
    public List<List<int>> lines { get; set; }
    public List<double> bets { get; set; }
    public int totalLines { get; set; }
}

[Serializable]
public class Player
{
    public double balance { get; set; }
}

[Serializable]
public class Features
{
    public WildExplosion wildExplosion { get; set; }
}

[Serializable]
public class WildExplosion
{
    public bool enabled { get; set; }
    public List<int> symbols { get; set; }
    public int maxWilds { get; set; }
    public int minWilds { get; set; }
    public double triggerProbability { get; set; }
}

[Serializable]
public class UiData
{
    public Paylines paylines { get; set; }
}

[Serializable]
public class Paylines
{
    public List<Symbol> symbols { get; set; }
}

[Serializable]
public class Symbol
{
    public int id { get; set; }
    public string name { get; set; }
    public List<int> multiplier { get; set; }
}

// Result Data Classes

[Serializable]
public class Payload
{
    public double betAmountPerLine { get; set; }
    public double totalBetAmount { get; set; }
    public double winAmount { get; set; }
    public List<LineWin> lineWins { get; set; }
    public WildExplosions wildExplosions { get; set; }
    public double grandTotalWin { get; set; }
    public double netReturnRatio { get; set; }
}

[Serializable]
public class LineWin
{
    public int lineIndex { get; set; }
    public List<MatchedPosition> matchedPositions { get; set; }
    public List<int> symbolIds { get; set; }
    public int basePayout { get; set; }
    public int winInCredits { get; set; }
    public double winInCash { get; set; }
    public int matchLength { get; set; }
    public int lineMultiplier { get; set; }
}

[Serializable]
public class MatchedPosition
{
    public int row { get; set; }
    public int col { get; set; }
}

[Serializable]
public class WildExplosions
{
    public bool triggered { get; set; }
    public List<List<string>> originalMatrix { get; set; }
    public List<List<string>> newMatrix { get; set; }
    public List<TriggerPosition> triggerPositions { get; set; }
    public List<AddedWildPosition> addedWildPositions { get; set; }
}

[Serializable]
public class TriggerPosition
{
    public int row { get; set; }
    public int col { get; set; }
}

[Serializable]
public class AddedWildPosition
{
    public int row { get; set; }
    public int col { get; set; }
}