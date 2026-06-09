-- Optional helper SQL script. Preferred setup is EF Core migrations.
-- Create the database manually if needed:
IF DB_ID('LeadDeskDb') IS NULL
BEGIN
    CREATE DATABASE LeadDeskDb;
END
GO
