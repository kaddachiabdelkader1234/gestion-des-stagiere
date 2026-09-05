package com.smartek.authservice.config;

import com.smartek.authservice.entity.User;
import com.smartek.authservice.enums.RoleType;
import com.smartek.authservice.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.CommandLineRunner;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

/**
 * Seeds exactly one ADMIN account on first boot from environment variables.
 * No-op if an admin already exists — safe to restart without creating duplicates.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class DataSeeder implements CommandLineRunner {

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;

    @Value("${admin.email:}")
    private String adminEmail;

    @Value("${admin.password:}")
    private String adminPassword;

    @Value("${admin.firstName:Admin}")
    private String adminFirstName;

    @Override
    @Transactional
    public void run(String... args) {
        // Skip if env vars not set
        if (adminEmail == null || adminEmail.isBlank()
                || adminPassword == null || adminPassword.isBlank()) {
            log.info("Admin seed skipped: ADMIN_EMAIL / ADMIN_PASSWORD not set");
            return;
        }

        // Idempotent: do nothing if this email already exists in any role
        if (userRepository.existsByEmail(adminEmail)) {
            log.info("Admin seed skipped: email {} already exists", adminEmail);
            return;
        }

        User admin = User.builder()
                .firstName(adminFirstName)
                .email(adminEmail)
                .password(passwordEncoder.encode(adminPassword))
                .role(RoleType.ADMIN)
                .experience(0)
                .build();

        userRepository.save(admin);
        log.info("ADMIN account seeded: {}", adminEmail);
    }
}
