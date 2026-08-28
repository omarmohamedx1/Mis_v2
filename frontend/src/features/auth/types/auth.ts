export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginFormValues extends LoginRequest {
  rememberMe: boolean;
}

export interface AuthenticatedUser {
  id: string;
  username: string;
  email: string;
  loginCode: string;
  fullName: string;
  department: string;
  role: string;
  roles: string[];
  permissions: string[];
}

export interface UserProfile { id: string; loginCode: string; username: string; email: string; fullName: string; department: string; roles: string[]; lastLoginAt?: string }

export interface AuthResponse {
  accessToken: string;
  user: AuthenticatedUser;
}
