import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { authService } from '../features/auth/services/authService';
import type { AuthResponse, AuthenticatedUser, LoginRequest } from '../features/auth/types/auth';
import { clearStoredAuth, getStoredAuth, persistAuth, updateStoredAuth } from '../utils/storage';

interface AuthContextValue {
  accessToken: string | null;
  user: AuthenticatedUser | null;
  isAuthenticated: boolean;
  login: (credentials: LoginRequest, rememberMe: boolean) => Promise<AuthenticatedUser>;
  logout: () => void;
  updateUser: (user: AuthenticatedUser) => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

interface AuthProviderProps {
  children: React.ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [auth, setAuth] = useState<AuthResponse | null>(() => getStoredAuth());

  const login = useCallback(async (credentials: LoginRequest, rememberMe: boolean) => {
    const response = await authService.login(credentials);
    persistAuth(response, rememberMe);
    setAuth(response);
    return response.user;
  }, []);

  const logout = useCallback(() => {
    clearStoredAuth();
    setAuth(null);
  }, []);

  const updateUser = useCallback((user: AuthenticatedUser) => {
    setAuth((current) => {
      if (!current) return current;
      const next = { ...current, user };
      updateStoredAuth(next);
      return next;
    });
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      accessToken: auth?.accessToken ?? null,
      user: auth?.user ?? null,
      isAuthenticated: Boolean(auth?.accessToken),
      login,
      logout,
      updateUser,
    }),
    [auth, login, logout, updateUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.');
  }

  return context;
}
