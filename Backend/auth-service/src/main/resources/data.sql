-- Fix role column to accept SPONSOR value
ALTER TABLE users MODIFY COLUMN role VARCHAR(30) NOT NULL;

