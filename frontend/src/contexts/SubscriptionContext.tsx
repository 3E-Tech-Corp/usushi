import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useAuth } from './AuthContext';
import api from '../services/api';

interface SubscriptionStatus {
  plan: string;
  status: string;
  stripeCustomerId: string | null;
  stripeSubscriptionId: string | null;
  currentPeriodEnd: string | null;
}

interface SubscriptionContextType {
  plan: string;
  status: string;
  isActive: boolean;
  isPro: boolean;
  isBusiness: boolean;
  currentPeriodEnd: string | null;
  isLoading: boolean;
  refresh: () => Promise<void>;
}

const SubscriptionContext = createContext<SubscriptionContextType | undefined>(undefined);

export function SubscriptionProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [subscriptionData, setSubscriptionData] = useState<SubscriptionStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const refresh = useCallback(async () => {
    if (!isAuthenticated) {
      setSubscriptionData(null);
      return;
    }
    setIsLoading(true);
    try {
      const data = await api.get<SubscriptionStatus>('/payments/status');
      setSubscriptionData(data);
    } catch {
      setSubscriptionData(null);
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const plan = subscriptionData?.plan ?? 'Free';
  const status = subscriptionData?.status ?? 'inactive';
  const isActive = status === 'active';
  const isPro = isActive && plan === 'Pro';
  const isBusiness = isActive && plan === 'Business';

  return (
    <SubscriptionContext.Provider
      value={{
        plan,
        status,
        isActive,
        isPro,
        isBusiness,
        currentPeriodEnd: subscriptionData?.currentPeriodEnd ?? null,
        isLoading,
        refresh,
      }}
    >
      {children}
    </SubscriptionContext.Provider>
  );
}

export function useSubscription() {
  const context = useContext(SubscriptionContext);
  if (context === undefined) {
    throw new Error('useSubscription must be used within a SubscriptionProvider');
  }
  return context;
}
