using NativeGuard.Core.Runtime;

namespace NativeGuard.Core.Tests;

[TestClass]
public sealed class SingleInstanceGuardTests
{
    [TestMethod]
    public void Acquire_AllowsOnlyOneOwnerForTheSameName()
    {
        string name = $@"Local\NativeGuard.Tests.{Guid.NewGuid():N}";

        using SingleInstanceGuard first = SingleInstanceGuard.Acquire(name);
        using SingleInstanceGuard second = SingleInstanceGuard.Acquire(name);

        Assert.IsTrue(first.IsPrimaryInstance);
        Assert.IsFalse(second.IsPrimaryInstance);
    }

    [TestMethod]
    public void Acquire_AllowsNewOwnerAfterPrimaryIsDisposed()
    {
        string name = $@"Local\NativeGuard.Tests.{Guid.NewGuid():N}";

        using (SingleInstanceGuard first = SingleInstanceGuard.Acquire(name))
        {
            Assert.IsTrue(first.IsPrimaryInstance);
        }

        using SingleInstanceGuard second = SingleInstanceGuard.Acquire(name);

        Assert.IsTrue(second.IsPrimaryInstance);
    }
}
