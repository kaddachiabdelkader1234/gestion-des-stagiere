package com.smartek.authservice.dto;

import com.smartek.authservice.enums.RoleType;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class AuthResponse {
    
    private String token;
    private String refreshToken;
    @Builder.Default
    private String type = "Bearer";
    private Long userId;
    private String email;
    private String firstName;
    private RoleType role;
    private String imageBase64; // Image en base64 pour le frontend
    private Integer experience;
    private String message;
}
