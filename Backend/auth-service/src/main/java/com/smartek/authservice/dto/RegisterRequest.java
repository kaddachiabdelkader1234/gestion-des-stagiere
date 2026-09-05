package com.smartek.authservice.dto;

import com.smartek.authservice.enums.RoleType;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RegisterRequest {
    
    private String imageBase64; // Accepter base64 string du frontend
    
    @NotBlank(message = "Le prénom est obligatoire")
    @Size(max = 50)
    private String firstName;
    
    @NotBlank(message = "L'email est obligatoire")
    @Email(message = "Format d'email invalide")
    private String email;
    
    @NotBlank(message = "Le mot de passe est obligatoire")
    @Size(min = 8, message = "Le mot de passe doit contenir au moins 8 caractères")
    private String password;
    
    private String phone;
    
    private Integer experience;
    
    // Role is intentionally nullable — the server always forces LEARNER for public registration.
    // Accepting the field (ignored) so old clients don't break, but never trusting it.
    private RoleType role;
}
