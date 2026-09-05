package com.smartek.authservice.controller;

import com.smartek.authservice.dto.AuthResponse;
import com.smartek.authservice.dto.CreateEncadrantRequest;
import com.smartek.authservice.dto.LoginRequest;
import com.smartek.authservice.dto.RegisterRequest;
import com.smartek.authservice.dto.UserSummaryResponse;
import com.smartek.authservice.enums.RoleType;
import com.smartek.authservice.service.AuthService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.Map;

/**
 * Auth endpoints. Reached by the browser as /api/v1/auth/** through the gateway, which rewrites
 * the prefix onto /api/auth/**.
 *
 * Deliberately no @CrossOrigin here: the gateway already sets Access-Control-Allow-Origin, and a
 * second value from this controller made the header "http://localhost:4200,*", which browsers
 * reject as invalid — logging in from Angular failed with a CORS error. CORS belongs to the one
 * component the browser actually talks to.
 */
@RestController
@RequestMapping("/api/auth")
@RequiredArgsConstructor
@Slf4j
public class AuthController {
    
    private final AuthService authService;
    
    @PostMapping("/register")
    public ResponseEntity<AuthResponse> register(@Valid @RequestBody RegisterRequest request) {
        log.info("Requête d'inscription reçue pour: {}", request.getEmail());
        try {
            AuthResponse response = authService.register(request);
            return ResponseEntity.status(HttpStatus.CREATED).body(response);
        } catch (RuntimeException e) {
            log.error("Erreur lors de l'inscription: {}", e.getMessage());
            // SECURITY: Never expose internal error details to the client.
            // Use a generic message to avoid information disclosure.
            return ResponseEntity.badRequest()
                    .body(AuthResponse.builder()
                            .message("Inscription impossible. Veuillez vérifier vos informations.")
                            .build());
        }
    }
    
    @PostMapping("/login")
    public ResponseEntity<AuthResponse> login(@Valid @RequestBody LoginRequest request) {
        log.info("Requête de connexion reçue pour: {}", request.getEmail());
        try {
            AuthResponse response = authService.login(request);
            return ResponseEntity.ok(response);
        } catch (RuntimeException e) {
            log.error("Erreur lors de la connexion: {}", e.getMessage());
            return ResponseEntity.status(HttpStatus.UNAUTHORIZED)
                    .body(AuthResponse.builder()
                            .message("Email ou mot de passe incorrect")
                            .build());
        }
    }
    
    /**
     * Admin-only endpoint to create an encadrant (TRAINER) account.
     * The gateway restricts this to ROLE_ADMIN. The role is hardcoded to TRAINER server-side.
     * Returns a temporary password shown once to the admin.
     */
    @PostMapping("/encadrants")
    public ResponseEntity<AuthResponse> createEncadrant(@Valid @RequestBody CreateEncadrantRequest request) {
        log.info("Création d'encadrant par admin: {}", request.getEmail());
        try {
            AuthResponse response = authService.createEncadrant(request);
            return ResponseEntity.status(HttpStatus.CREATED).body(response);
        } catch (RuntimeException e) {
            log.error("Erreur lors de la création de l'encadrant: {}", e.getMessage());
            // SECURITY: Never expose internal error details to the client.
            return ResponseEntity.badRequest()
                    .body(AuthResponse.builder()
                            .message("Création impossible. Veuillez vérifier les informations.")
                            .build());
        }
    }

    @PostMapping("/refresh")
    public ResponseEntity<AuthResponse> refresh(@RequestBody Map<String, String> body) {
        String refreshToken = body.get("refreshToken");
        if (refreshToken == null || refreshToken.isBlank()) {
            return ResponseEntity.badRequest()
                    .body(AuthResponse.builder().message("Refresh token manquant").build());
        }
        try {
            AuthResponse response = authService.refresh(refreshToken);
            return ResponseEntity.ok(response);
        } catch (RuntimeException e) {
            log.error("Erreur lors du refresh: {}", e.getMessage());
            return ResponseEntity.status(HttpStatus.UNAUTHORIZED)
                    .body(AuthResponse.builder().message("Refresh token invalide ou expiré").build());
        }
    }

    @GetMapping("/health")
    public ResponseEntity<String> health() {
        return ResponseEntity.ok("Auth Service is running");
    }
    
    @GetMapping("/validate/{userId}")
    public ResponseEntity<Boolean> validateUser(@PathVariable Long userId) {
        log.info("Validation de l'utilisateur avec ID: {}", userId);
        try {
            boolean isValid = authService.validateUser(userId);
            return ResponseEntity.ok(isValid);
        } catch (Exception e) {
            log.error("Erreur lors de la validation de l'utilisateur: {}", e.getMessage());
            return ResponseEntity.ok(false);
        }
    }

    /**
     * Lists users filtered by role — e.g. {@code /api/v1/auth/users?role=TRAINER} to populate the
     * admin's "assign an encadrant" dropdown.
     *
     * Reached only by an ADMIN: the gateway restricts {@code /api/v1/auth/users/**} the same way it
     * restricts {@code /user/**} and {@code /validate/**}, because this exposes account data.
     *
     * Returns summaries (no token, no profile image) — see {@link UserSummaryResponse}.
     */
    @GetMapping("/users")
    public ResponseEntity<List<UserSummaryResponse>> getUsersByRole(@RequestParam RoleType role) {
        log.info("Liste des utilisateurs pour le rôle: {}", role);
        return ResponseEntity.ok(authService.getUsersByRole(role));
    }
    
    @GetMapping("/user/{userId}")
    public ResponseEntity<AuthResponse> getUserById(@PathVariable Long userId) {
        log.info("Récupération des données de l'utilisateur avec ID: {}", userId);
        try {
            AuthResponse response = authService.getUserById(userId);
            return ResponseEntity.ok(response);
        } catch (RuntimeException e) {
            log.error("Erreur lors de la récupération de l'utilisateur: {}", e.getMessage());
            return ResponseEntity.status(HttpStatus.NOT_FOUND)
                    .body(AuthResponse.builder()
                            .message("Utilisateur non trouvé")
                            .build());
        }
    }
}
