using System;
using C2VM.TrafficLightsEnhancement.Utils;
using Unity.Entities;
using Xunit;

namespace TrafficLightsEnhancement.Ecs.Tests;

public sealed class EntityQueryUtilsTests
{
    [Fact]
    public void Try_get_entity_query_returns_false_for_missing_field()
    {
        var holder = new QueryHolder();

        bool found = EntityQueryUtils.TryGetEntityQuery(holder, "m_MissingQuery", out _, out string error);

        Assert.False(found);
        Assert.Contains("m_MissingQuery", error);
    }

    [Fact]
    public void Get_entity_query_throws_controlled_exception_for_missing_field()
    {
        var holder = new QueryHolder();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EntityQueryUtils.GetEntityQuery(holder, "m_MissingQuery"));

        Assert.Contains("m_MissingQuery", exception.Message);
    }

    [Fact]
    public void Try_set_entity_query_returns_false_for_missing_field()
    {
        var holder = new QueryHolder();

        bool updated = EntityQueryUtils.TrySetEntityQuery(holder, "m_MissingQuery", default, out string error);

        Assert.False(updated);
        Assert.Contains("m_MissingQuery", error);
    }

    [Fact]
    public void Try_get_and_set_entity_query_keep_existing_private_field_behavior()
    {
        var holder = new QueryHolder();

        Assert.True(EntityQueryUtils.TryGetEntityQuery(holder, "m_Query", out EntityQuery query, out string getError));
        Assert.Null(getError);
        Assert.Equal(default, query);

        Assert.True(EntityQueryUtils.TrySetEntityQuery(holder, "m_Query", default, out string setError));
        Assert.Null(setError);
    }

    private sealed class QueryHolder
    {
#pragma warning disable CS0169
        private EntityQuery m_Query;
#pragma warning restore CS0169
    }
}
