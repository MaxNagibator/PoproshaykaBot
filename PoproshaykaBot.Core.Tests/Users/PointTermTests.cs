using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Core.Tests.Users;

[TestFixture]
public sealed class PointTermTests
{
    private readonly PointTerm _term = new()
    {
        Singular = "балл",
        Few = "балла",
        Many = "баллов",
    };

    [TestCase(1, "балл")]
    [TestCase(21, "балл")]
    [TestCase(101, "балл")]
    [TestCase(2, "балла")]
    [TestCase(3, "балла")]
    [TestCase(4, "балла")]
    [TestCase(22, "балла")]
    [TestCase(34, "балла")]
    [TestCase(0, "баллов")]
    [TestCase(5, "баллов")]
    [TestCase(10, "баллов")]
    [TestCase(11, "баллов")]
    [TestCase(12, "баллов")]
    [TestCase(14, "баллов")]
    [TestCase(15, "баллов")]
    [TestCase(20, "баллов")]
    [TestCase(111, "баллов")]
    [TestCase(112, "баллов")]
    [TestCase(114, "баллов")]
    [TestCase(115, "баллов")]
    [TestCase(-1, "балл")]
    [TestCase(-5, "баллов")]
    [TestCase(long.MinValue, "баллов")]
    [TestCase(long.MaxValue, "баллов")]
    public void ForCount_ReturnsCorrectRussianForm(long count, string expected)
    {
        Assert.That(_term.ForCount(count), Is.EqualTo(expected));
    }
}
