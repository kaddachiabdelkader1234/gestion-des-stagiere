import { test, expect } from '@playwright/test';

test.describe('Authentication Flow', () => {
  test('should display sign-in page', async ({ page }) => {
    await page.goto('/auth/sign-in');
    await expect(page.locator('h1')).toContainText('Connexion');
  });

  test('should display sign-up page', async ({ page }) => {
    await page.goto('/auth/sign-up');
    await expect(page.locator('h1')).toContainText('Créer un compte');
  });

  test('should show error on wrong password', async ({ page }) => {
    await page.goto('/auth/sign-in');
    await page.fill('input[formControlName="email"]', 'wrong@test.com');
    await page.fill('input[formControlName="password"]', 'wrongpassword');
    await page.click('button[type="submit"]');
    await expect(page.locator('.text-red-500')).toBeVisible();
  });

  test('should register as learner and redirect to dashboard', async ({ page }) => {
    const timestamp = Date.now();
    const testEmail = `test-${timestamp}@stb.tn`;

    await page.goto('/auth/sign-up');
    await page.fill('input[formControlName="firstName"]', 'TestUser');
    await page.fill('input[formControlName="email"]', testEmail);
    await page.fill('input[formControlName="password"]', 'Test1234!');
    await page.fill('input[formControlName="confirmPassword"]', 'Test1234!');
    await page.click('button[type="submit"]');

    // Should redirect to dashboard
    await expect(page).toHaveURL(/.*dashboard/);
  });

  test('should login as admin and see admin sidebar', async ({ page }) => {
    await page.goto('/auth/sign-in');
    await page.fill('input[formControlName="email"]', 'admin@stb.tn');
    await page.fill('input[formControlName="password"]', 'Admin123!');
    await page.click('button[type="submit"]');

    // Should redirect to dashboard
    await expect(page).toHaveURL(/.*dashboard/);

    // Should see admin menu items
    await expect(page.locator('text=Candidatures')).toBeVisible();
    await expect(page.locator('text=Créer un encadrant')).toBeVisible();
  });
});

test.describe('Navigation', () => {
  test('should navigate between sign-in and sign-up', async ({ page }) => {
    await page.goto('/auth/sign-in');
    await page.click('text=S\'inscrire');
    await expect(page).toHaveURL(/.*sign-up/);

    await page.click('text=Se connecter');
    await expect(page).toHaveURL(/.*sign-in/);
  });

  test('should redirect unauthenticated users to sign-in', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/.*sign-in/);
  });
});
