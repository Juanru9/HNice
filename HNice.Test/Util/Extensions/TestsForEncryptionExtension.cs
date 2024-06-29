using FluentAssertions;
using HNice.Util.Extensions;

namespace HNice.Test.Util.Extensions;

public class TestsForEncryptionExtension
{
    //See https://github.com/habb0/docs/blob/master/guides/The%20need%20to%20know%20functions%20of%20Habbo%20emulators.md
    [Theory]
    [InlineData(8123732, "hUuoG")]
    [InlineData(10, "RB")]
    [InlineData(39, "SI")]
    [InlineData(843, "[RC")]
    [InlineData(7, "SA")]
    [InlineData(-1, "M")]
    public void ShouldVL64EncodeAInteger(int integerToVL64Encode, string expectedVL64EncodedString) 
    {
        // Act
        var result = integerToVL64Encode.EncodeVL64();

        // Assert
        result.Should().Be(expectedVL64EncodedString);
    }

    [Theory]
    [InlineData("RB", new int[] { 10 })]
    [InlineData("IQAJPCSAIJPCHJHJXKDX]BIIJQAPCRE", new int[] { 1, 5, 2, 12, 7, 1, 2, 12, 0, 2, 0, 2, 1068, 628, 1, 1, 2, 5, 12, 22 })]
    public void ShouldVL64DecodeAEncryptedString(string integerToVL64Encode, int[] expectedVL64Ints)
    {
        // Arrange & Act
        var decodedResults = integerToVL64Encode.DecodeVL64();

        // Assert
        for (int i = 0; i< decodedResults.Count(); i++) 
        {
            decodedResults.ElementAt(i).IntCodeValue.Should().Be(expectedVL64Ints[i]);
        }
    }

}
