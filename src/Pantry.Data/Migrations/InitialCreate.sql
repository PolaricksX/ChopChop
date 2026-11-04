CREATE TABLE [PantryItems] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_PantryItems] PRIMARY KEY,
    [OwnerId] nvarchar(200) NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Type] int NOT NULL,
    [CustomCategory] nvarchar(60) NULL,
    [Notes] nvarchar(500) NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [Unit] int NOT NULL,
    [ExpiresAt] datetimeoffset NULL,
    [Availability] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NOT NULL,
    [Version] rowversion NOT NULL,
    [OwnerIdNormalized] nvarchar(200) NOT NULL,
    [NameNormalized] nvarchar(120) NOT NULL
);

CREATE UNIQUE INDEX [UX_PantryItems_ActiveOwnerIngredientType]
    ON [PantryItems] ([OwnerIdNormalized], [NameNormalized], [Type])
    WHERE [Availability] = 0;

CREATE TABLE [OutboxMessages] (
    [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_OutboxMessages] PRIMARY KEY,
    [Type] nvarchar(200) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [PublishedAt] datetimeoffset NULL
);

CREATE INDEX [IX_OutboxMessages_PublishedAt_OccurredAt]
    ON [OutboxMessages] ([PublishedAt], [OccurredAt]);
