using Microsoft.AspNetCore.Http;
using Pantry.Api.Contracts;
using Pantry.Domain.Enums;

namespace Pantry.Tests;

public sealed class PantryApiContractTests
{
    [Fact]
    public void Query_defaults_and_bounds_are_deterministic()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?page=0&pageSize=999&type=fruits&availability=consumed");

        var query = PantryQuery.From(context.Request);

        Assert.Equal(1, query.Page);
        Assert.Equal(100, query.PageSize);
        Assert.Equal(PantryType.Fruits, query.Type);
        Assert.Equal(Availability.Consumed, query.Availability);
    }

    [Fact]
    public void Invalid_enum_and_date_values_are_ignored()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?type=unknown&availability=bad&expiresBefore=not-a-date");

        var query = PantryQuery.From(context.Request);

        Assert.Null(query.Type);
        Assert.Null(query.Availability);
        Assert.Null(query.ExpiresBefore);
    }
}
