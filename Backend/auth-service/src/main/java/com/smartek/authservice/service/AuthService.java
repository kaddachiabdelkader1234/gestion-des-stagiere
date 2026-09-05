package com.smartek.authservice.service;

import com.smartek.authservice.dto.AuthResponse;
import com.smartek.authservice.dto.LoginRequest;
import com.smartek.authservice.dto.RegisterRequest;
import com.smartek.authservice.dto.UserSummaryResponse;
import com.smartek.authservice.entity.User;
import com.smartek.authservice.enums.RoleType;
import com.smartek.authservice.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.security.authentication.AuthenticationManager;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.Base64;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

@Service
@RequiredArgsConstructor
@Slf4j
public class AuthService {
    
    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtService jwtService;
    private final AuthenticationManager authenticationManager;
    private final JdbcTemplate jdbcTemplate;
    
    @Transactional
    public AuthResponse register(RegisterRequest request) {
        log.info("Tentative d'inscription pour l'email: {}", request.getEmail());
        
        // Vérifier si l'email existe déjà
        if (userRepository.existsByEmail(request.getEmail())) {
            throw new RuntimeException("Cet email est déjà utilisé");
        }
        
        // SECURITY FIX: Always force LEARNER role for public registration.
        // Never trust the client-provided role — a direct API call could set
        // role=ADMIN and bypass every downstream authorization check.
        RoleType assignedRole = RoleType.LEARNER;
        log.info("Rôle assigné: {} (toujours LEARNER pour l'inscription publique)", assignedRole);
        
        // Créer le nouvel utilisateur
        User user = User.builder()
                .firstName(request.getFirstName())
                .email(request.getEmail())
                .password(passwordEncoder.encode(request.getPassword()))
                .phone(request.getPhone())
                .role(assignedRole)
                .image(convertBase64ToBytes(request.getImageBase64()))
                .experience(request.getExperience() != null ? request.getExperience() : 0)
                .build();
        
        User savedUser = userRepository.save(user);
        log.info("Utilisateur créé avec succès: {}", savedUser.getEmail());
        
        // Générer le token JWT
        Map<String, Object> claims = new HashMap<>();
        claims.put("role", savedUser.getRole().name());
        claims.put("userId", savedUser.getUserId());
        
        String token = jwtService.generateToken(savedUser.getEmail(), claims);
        String refreshToken = jwtService.generateRefreshToken(savedUser.getEmail(), claims);
        savedUser.setRefreshToken(refreshToken);
        userRepository.save(savedUser);
        
        return AuthResponse.builder()
                .token(token)
                .refreshToken(refreshToken)
                .userId(savedUser.getUserId())
                .email(savedUser.getEmail())
                .firstName(savedUser.getFirstName())
                .role(savedUser.getRole())
                .imageBase64(convertBytesToBase64(savedUser.getImage()))
                .experience(savedUser.getExperience())
                .message("Inscription réussie")
                .build();
    }
    
    @Transactional
    public AuthResponse login(LoginRequest request) {
        log.info("Tentative de connexion pour l'email: {}", request.getEmail());
        
        // Authentifier l'utilisateur
        authenticationManager.authenticate(
                new UsernamePasswordAuthenticationToken(
                        request.getEmail(),
                        request.getPassword()
                )
        );
        
        // Récupérer l'utilisateur
        User user = userRepository.findByEmail(request.getEmail())
                .orElseThrow(() -> new RuntimeException("Utilisateur non trouvé"));
        
        // Générer le token JWT + refresh token
        Map<String, Object> claims = new HashMap<>();
        claims.put("role", user.getRole().name());
        claims.put("userId", user.getUserId());
        
        String token = jwtService.generateToken(user.getEmail(), claims);
        String refreshToken = jwtService.generateRefreshToken(user.getEmail(), claims);
        user.setRefreshToken(refreshToken);
        userRepository.save(user);
        
        log.info("Connexion réussie pour: {}", user.getEmail());
        
        return AuthResponse.builder()
                .token(token)
                .refreshToken(refreshToken)
                .userId(user.getUserId())
                .email(user.getEmail())
                .firstName(user.getFirstName())
                .role(user.getRole())
                .imageBase64(convertBytesToBase64(user.getImage()))
                .experience(user.getExperience())
                .message("Connexion réussie")
                .build();
    }
    
    public boolean validateUser(Long userId) {
        return userRepository.existsById(userId);
    }

    /**
     * Validates a refresh token and issues a new access + refresh token pair.
     * The old refresh token is invalidated (one-time use).
     *
     * Uses an atomic SQL UPDATE (via {@code atomicSwapRefreshToken}) to prevent the TOCTOU race
     * where two concurrent requests both read the old token, both pass the check, and both write
     * new tokens. The atomic update returns 0 rows affected if the token was already consumed.
     *
     * The repository method uses {@code @Modifying(clearAutomatically = true)} so Hibernate's
     * persistence context is cleared after the UPDATE. This prevents Hibernate from flushing the
     * stale entity loaded by {@code findByEmail} back to the DB at commit time, which would
     * overwrite the atomic UPDATE with the old token value.
     */
    @Transactional
    public AuthResponse refresh(String refreshToken) {
        // 1. Extract email from the JWT — no DB call needed for this.
        String email = jwtService.extractUsername(refreshToken);
        
        // 2. Load the user to get claims for token generation.
        User user = userRepository.findByEmail(email)
                .orElseThrow(() -> new RuntimeException("Utilisateur non trouvé"));
        
        // 3. Generate new tokens with proper claims.
        Map<String, Object> claims = new HashMap<>();
        claims.put("role", user.getRole().name());
        claims.put("userId", user.getUserId());
        
        String newToken = jwtService.generateToken(user.getEmail(), claims);
        String newRefreshToken = jwtService.generateRefreshToken(user.getEmail(), claims);
        
        // 4. Atomically check that the stored token matches AND replace it.
        //    Uses JdbcTemplate directly to bypass Hibernate's persistence context entirely.
        //    The UPDATE ... WHERE refreshToken = ? is a single SQL statement — atomic at the DB level.
        //    Returns 1 if the swap succeeded, 0 if the token was already consumed.
        int rowsAffected = jdbcTemplate.update(
                "UPDATE users SET refresh_token = ? WHERE email = ? AND refresh_token = ?",
                newRefreshToken, email, refreshToken);
        log.info("Refresh token swap: email={}, rowsAffected={}", email, rowsAffected);
        
        if (rowsAffected == 0) {
            throw new RuntimeException("Refresh token invalide ou déjà utilisé");
        }
        
        return AuthResponse.builder()
                .token(newToken)
                .refreshToken(newRefreshToken)
                .userId(user.getUserId())
                .email(user.getEmail())
                .firstName(user.getFirstName())
                .role(user.getRole())
                .imageBase64(convertBytesToBase64(user.getImage()))
                .experience(user.getExperience())
                .message("Token rafraîchi")
                .build();
    }

    /**
     * Creates a TRAINER (encadrant) account. Admin-only — the controller must enforce this.
     * A temporary password is generated and returned once in the response.
     */
    @Transactional
    public AuthResponse createEncadrant(com.smartek.authservice.dto.CreateEncadrantRequest request) {
        if (userRepository.existsByEmail(request.getEmail())) {
            throw new RuntimeException("Cet email est déjà utilisé");
        }

        String tempPassword = generateTempPassword();

        User user = User.builder()
                .firstName(request.getFirstName())
                .email(request.getEmail())
                .password(passwordEncoder.encode(tempPassword))
                .role(RoleType.TRAINER)
                .experience(0)
                .build();

        User savedUser = userRepository.save(user);
        log.info("Encadrant créé par admin: {} (email: {})", savedUser.getFirstName(), savedUser.getEmail());

        Map<String, Object> claims = new HashMap<>();
        claims.put("role", savedUser.getRole().name());
        claims.put("userId", savedUser.getUserId());

        String token = jwtService.generateToken(savedUser.getEmail(), claims);

        return AuthResponse.builder()
                .token(token)
                .userId(savedUser.getUserId())
                .email(savedUser.getEmail())
                .firstName(savedUser.getFirstName())
                .role(savedUser.getRole())
                .message("Encadrant créé. Mot de passe temporaire: " + tempPassword)
                .build();
    }

    private String generateTempPassword() {
        String chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        StringBuilder sb = new StringBuilder();
        java.util.Random random = new java.util.Random();
        for (int i = 0; i < 12; i++) {
            sb.append(chars.charAt(random.nextInt(chars.length())));
        }
        return sb.toString();
    }

    /**
     * Lists users of one role, as summaries.
     *
     * Backs the admin's encadrant dropdown (role {@code TRAINER}). Returns
     * {@link UserSummaryResponse} rather than {@code AuthResponse} so no token field or base64
     * image is ever part of a list payload.
     */
    public List<UserSummaryResponse> getUsersByRole(RoleType role) {
        return userRepository.findByRole(role).stream()
                .map(user -> UserSummaryResponse.builder()
                        .userId(user.getUserId())
                        .email(user.getEmail())
                        .firstName(user.getFirstName())
                        .role(user.getRole())
                        .build())
                .toList();
    }
    
    public AuthResponse getUserById(Long userId) {
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new RuntimeException("Utilisateur non trouvé"));
        
        return AuthResponse.builder()
                .userId(user.getUserId())
                .email(user.getEmail())
                .firstName(user.getFirstName())
                .role(user.getRole())
                .imageBase64(convertBytesToBase64(user.getImage()))
                .experience(user.getExperience())
                .message("Données utilisateur récupérées")
                .build();
    }
    
    private byte[] convertBase64ToBytes(String base64String) {
        if (base64String == null || base64String.isEmpty()) {
            return null;
        }
        try {
            return Base64.getDecoder().decode(base64String);
        } catch (IllegalArgumentException e) {
            log.error("Erreur lors de la conversion de l'image base64", e);
            return null;
        }
    }
    
    private String convertBytesToBase64(byte[] bytes) {
        if (bytes == null || bytes.length == 0) {
            return null;
        }
        try {
            return Base64.getEncoder().encodeToString(bytes);
        } catch (Exception e) {
            log.error("Erreur lors de la conversion des bytes en base64", e);
            return null;
        }
    }
}
