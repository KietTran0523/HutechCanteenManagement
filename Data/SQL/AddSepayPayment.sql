-- Chạy file này trên database QuanLyCanTeenHutech nếu bạn không dùng được dotnet ef database update.
-- Nếu đã chạy migration AddSepayPayment rồi thì không cần chạy file SQL này nữa.

IF COL_LENGTH('Orders', 'PaymentCode') IS NULL
BEGIN
    ALTER TABLE Orders ADD PaymentCode nvarchar(50) NULL;
END

IF COL_LENGTH('Orders', 'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE Orders ADD PaymentMethod nvarchar(50) NOT NULL CONSTRAINT DF_Orders_PaymentMethod DEFAULT N'Sepay';
END

IF COL_LENGTH('Orders', 'PaymentStatus') IS NULL
BEGIN
    ALTER TABLE Orders ADD PaymentStatus nvarchar(50) NOT NULL CONSTRAINT DF_Orders_PaymentStatus DEFAULT N'Unpaid';
END

IF COL_LENGTH('Orders', 'PaidAt') IS NULL
BEGIN
    ALTER TABLE Orders ADD PaidAt datetime2 NULL;
END

IF COL_LENGTH('Orders', 'SepayTransactionId') IS NULL
BEGIN
    ALTER TABLE Orders ADD SepayTransactionId nvarchar(100) NULL;
END

IF OBJECT_ID('SepayTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE SepayTransactions (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SepayTransactions PRIMARY KEY,
        SepayId nvarchar(100) NOT NULL,
        Gateway nvarchar(50) NULL,
        TransactionDate datetime2 NULL,
        AccountNumber nvarchar(50) NULL,
        SubAccount nvarchar(50) NULL,
        Code nvarchar(100) NULL,
        Content nvarchar(max) NULL,
        TransferType nvarchar(20) NULL,
        Description nvarchar(max) NULL,
        TransferAmount decimal(18, 2) NOT NULL,
        Accumulated decimal(18, 2) NULL,
        ReferenceCode nvarchar(100) NULL,
        RawBody nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_PaymentCode' AND object_id = OBJECT_ID('Orders'))
BEGIN
    CREATE UNIQUE INDEX IX_Orders_PaymentCode ON Orders(PaymentCode) WHERE PaymentCode IS NOT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SepayTransactions_SepayId' AND object_id = OBJECT_ID('SepayTransactions'))
BEGIN
    CREATE UNIQUE INDEX IX_SepayTransactions_SepayId ON SepayTransactions(SepayId);
END
