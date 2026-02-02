import { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { Link } from 'react-router-dom';
import api from '../services/api';

interface StripeConfig {
  publishableKey: string;
  priceIds: {
    pro: string;
    business: string;
  };
}

const tiers = [
  {
    name: 'Free',
    price: '$0',
    period: '/month',
    description: 'Get started with the basics',
    features: [
      'Basic features',
      'Community support',
      'Up to 100 requests/day',
      '1 project',
    ],
    cta: 'Current Plan',
    highlighted: false,
    priceKey: null as string | null,
  },
  {
    name: 'Pro',
    price: '$19',
    period: '/month',
    description: 'For professionals who need more',
    features: [
      'Everything in Free',
      'Priority support',
      'Unlimited requests',
      '10 projects',
      'Advanced analytics',
      'API access',
    ],
    cta: 'Subscribe to Pro',
    highlighted: true,
    priceKey: 'pro',
  },
  {
    name: 'Business',
    price: '$49',
    period: '/month',
    description: 'For teams and organizations',
    features: [
      'Everything in Pro',
      'Dedicated support',
      'Unlimited projects',
      'Custom integrations',
      'SSO & SAML',
      'SLA guarantee',
      'Team management',
    ],
    cta: 'Subscribe to Business',
    highlighted: false,
    priceKey: 'business',
  },
];

export default function Pricing() {
  const { isAuthenticated } = useAuth();
  const [config, setConfig] = useState<StripeConfig | null>(null);
  const [loading, setLoading] = useState<string | null>(null);

  useEffect(() => {
    api.get<StripeConfig>('/payments/config').then(setConfig).catch(() => {});
  }, []);

  const handleSubscribe = async (priceKey: string) => {
    if (!isAuthenticated) {
      window.location.href = '/login';
      return;
    }

    if (!config) return;

    const priceId = priceKey === 'pro' ? config.priceIds.pro : config.priceIds.business;
    setLoading(priceKey);

    try {
      const { url } = await api.post<{ url: string }>('/payments/checkout', {
        priceId,
        successUrl: `${window.location.origin}/payment/success`,
        cancelUrl: `${window.location.origin}/payment/cancel`,
      });
      window.location.href = url;
    } catch {
      alert('Failed to start checkout. Please try again.');
    } finally {
      setLoading(null);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900">
      {/* Header */}
      <div className="py-8 px-4">
        <div className="max-w-5xl mx-auto flex items-center justify-between">
          <Link to="/" className="text-xl font-bold text-white hover:text-gray-300 transition-colors">
            ← ProjectTemplate
          </Link>
          {!isAuthenticated && (
            <Link
              to="/login"
              className="text-sm text-gray-400 hover:text-white transition-colors"
            >
              Sign in →
            </Link>
          )}
        </div>
      </div>

      {/* Pricing Header */}
      <div className="text-center py-12 px-4">
        <h1 className="text-4xl font-bold text-white mb-4">
          Simple, transparent pricing
        </h1>
        <p className="text-gray-400 text-lg max-w-2xl mx-auto">
          Choose the plan that fits your needs. Upgrade or downgrade at any time.
        </p>
      </div>

      {/* Pricing Cards */}
      <div className="max-w-5xl mx-auto px-4 pb-20">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          {tiers.map((tier) => (
            <div
              key={tier.name}
              className={`rounded-2xl p-8 flex flex-col ${
                tier.highlighted
                  ? 'bg-blue-600 ring-2 ring-blue-400 scale-105'
                  : 'bg-gray-800 border border-gray-700'
              }`}
            >
              <h3
                className={`text-lg font-semibold ${
                  tier.highlighted ? 'text-blue-100' : 'text-gray-400'
                }`}
              >
                {tier.name}
              </h3>
              <div className="mt-4 flex items-baseline">
                <span className="text-4xl font-bold text-white">{tier.price}</span>
                <span
                  className={`ml-1 text-sm ${
                    tier.highlighted ? 'text-blue-200' : 'text-gray-500'
                  }`}
                >
                  {tier.period}
                </span>
              </div>
              <p
                className={`mt-2 text-sm ${
                  tier.highlighted ? 'text-blue-100' : 'text-gray-400'
                }`}
              >
                {tier.description}
              </p>

              <ul className="mt-8 space-y-3 flex-1">
                {tier.features.map((feature) => (
                  <li key={feature} className="flex items-start">
                    <svg
                      className={`w-5 h-5 mr-3 flex-shrink-0 ${
                        tier.highlighted ? 'text-blue-200' : 'text-green-400'
                      }`}
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M5 13l4 4L19 7"
                      />
                    </svg>
                    <span
                      className={`text-sm ${
                        tier.highlighted ? 'text-white' : 'text-gray-300'
                      }`}
                    >
                      {feature}
                    </span>
                  </li>
                ))}
              </ul>

              <div className="mt-8">
                {tier.priceKey ? (
                  <button
                    onClick={() => handleSubscribe(tier.priceKey!)}
                    disabled={loading === tier.priceKey}
                    className={`w-full py-3 px-6 rounded-lg font-medium transition-colors ${
                      tier.highlighted
                        ? 'bg-white text-blue-600 hover:bg-gray-100'
                        : 'bg-blue-600 text-white hover:bg-blue-700'
                    } disabled:opacity-50 disabled:cursor-not-allowed`}
                  >
                    {loading === tier.priceKey ? 'Redirecting...' : tier.cta}
                  </button>
                ) : (
                  <div className="w-full py-3 px-6 rounded-lg font-medium text-center bg-gray-700 text-gray-400">
                    {tier.cta}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
