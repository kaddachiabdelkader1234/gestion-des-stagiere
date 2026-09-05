import { test, expect } from '@playwright/test';

test.describe('Candidature Flow', () => {
  test.beforeEach(async ({ page }) => {
    // Login as learner before each test
    await page.goto('/auth/sign-in');
    await page.fill('input[formControlName="email"]', 'learner@stb.tn');
    await page.fill('input[formControlName="password"]', 'Learner123!');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/.*dashboard/);
  });

  test('should display ma candidature page', async ({ page }) => {
    await page.goto('/dashboard/ma-candidature');
    await expect(page.locator('h1, h2')).toContainText(/candidature/i);
  });

  test('should show candidature form for new stagiaire', async ({ page }) => {
    await page.goto('/dashboard/ma-candidature');
    // Should have form fields
    await expect(page.locator('form')).toBeVisible();
  });
});

test.describe('Admin Candidature Management', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto('/auth/sign-in');
    await page.fill('input[formControlName="email"]', 'admin@stb.tn');
    await page.fill('input[formControlName="password"]', 'Admin123!');
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL(/.*dashboard/);
  });

  test('should display candidatures list', async ({ page }) => {
    await page.goto('/dashboard/candidatures');
    await expect(page.locator('h1, h2')).toContainText(/candidature/i);
  });
});
