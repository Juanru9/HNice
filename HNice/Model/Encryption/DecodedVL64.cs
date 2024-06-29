
namespace HNice.Model.Encryption;

public class DecodedVL64
{
    public string StringCodeValue { get; set; }
    public int IntCodeValue { get; set; }

    public DecodedVL64(string stringCodeValue, int intCodeValue) 
    {
        StringCodeValue = stringCodeValue;
        IntCodeValue = intCodeValue;
    }

}
