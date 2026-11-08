using Microsoft.EntityFrameworkCore.Infrastructure;
using Pantry.Data.Context;

namespace Pantry.Data.Migrations;

[DbContext(typeof(PantryDbContext))]
public partial class PantryDataInitial;
