import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useSubscription } from '../contexts/SubscriptionContext';
import { Link } from 'react-router-dom';
import api from '../services/api';

export default function Account() {
  const { user } = useAuth();
  const { plan, status, isActive, currentPeriodEnd, isLoading } = useSubscription();
  const [portalLoading, setPortalLoading] = useState(false);

  const handleManageSubscription = async () => {
    setPortalLoading(true);
    try {
      const { url } = await api.post<{ url: string }>('/payments/portal', {
        returnUrl: `${window.location.origin}/account`,
      });
      window.location.href = url;
    } catch {
      alert('Failed to open billing portal. Please try again.');
    } finally {
      setPortalLoading(false);
    }
  };

  const formatDate = (dateStr: string | null) => {
    if (!dateStr) return 'N/A';
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  const statusColor = (s: string) => {
    switch (s) {
      case 'active':
        return 'text-green-400 bg-green-400/10';
      case 'past_due':
        return 'text-yellow-400 bg-yellow-400/10';
      case 'canceled':
        return 'text-red-400 bg-red-400/10';
      default:
        return 'text-gray-400 bg-gray-400/10';
    }
  };

  if (isLoading) {
    return (
      <div>
        <h1 className="text-2xl font-bold text-white mb-6">Account & Billing</h1>
        <div className="text-gray-400">Loading subscription info...</div>
      </div>
    );
  }

  return (
    <div>
      <h1 className="text-2xl font-bold text-white mb-6">Account & Billing</h1>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Profile Card */}
        <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
          <h2 className="text-lg font-semibold text-white mb-4">Profile</h2>
          <div className="space-y-3">
            <div>
              <span className="text-sm text-gray-400">Username</span>
              <p className="text-white font-medium">{user?.username}</p>
            </div>
            <div>
              <span className="text-sm text-gray-400">Email</span>
              <p className="text-white font-medium">{user?.email}</p>
            </div>
            <div>
              <span className="text-sm text-gray-400">Role</span>
              <p className="text-white font-medium">{user?.role}</p>
            </div>
          </div>
        </div>

        {/* Subscription Card */}
        <div className="bg-gray-800 rounded-xl p-6 border border-gray-700">
          <h2 className="text-lg font-semibold text-white mb-4">Subscription</h2>
          <div className="space-y-3">
            <div>
              <span className="text-sm text-gray-400">Current Plan</span>
              <p className="text-white font-medium text-xl">{plan}</p>
            </div>
            <div>
              <span className="text-sm text-gray-400">Status</span>
              <p>
                <span
                  className={`inline-block px-2.5 py-1 rounded-full text-xs font-medium capitalize ${statusColor(status)}`}
                >
                  {status}
                </span>
              </p>
            </div>
            {isActive && currentPeriodEnd && (
              <div>
                <span className="text-sm text-gray-400">Next Billing Date</span>
                <p className="text-white font-medium">{formatDate(currentPeriodEnd)}</p>
              </div>
            )}
          </div>

          <div className="mt-6 space-y-3">
            {isActive ? (
              <button
                onClick={handleManageSubscription}
                disabled={portalLoading}
                className="w-full py-2.5 px-4 rounded-lg bg-gray-700 text-white font-medium hover:bg-gray-600 transition-colors disabled:opacity-50"
              >
                {portalLoading ? 'Opening...' : 'Manage Subscription'}
              </button>
            ) : (
              <Link
                to="/pricing"
                className="block w-full py-2.5 px-4 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 transition-colors text-center"
              >
                Upgrade Plan
              </Link>
            )}
          </div>
        </div>
      </div>

      {/* Quick Links */}
      {isActive && (
        <div className="mt-6 bg-gray-800 rounded-xl p-6 border border-gray-700">
          <h2 className="text-lg font-semibold text-white mb-4">Quick Actions</h2>
          <div className="flex flex-wrap gap-3">
            <button
              onClick={handleManageSubscription}
              disabled={portalLoading}
              className="px-4 py-2 rounded-lg bg-gray-700 text-sm text-gray-300 hover:bg-gray-600 hover:text-white transition-colors disabled:opacity-50"
            >
              Update Payment Method
            </button>
            <Link
              to="/pricing"
              className="px-4 py-2 rounded-lg bg-gray-700 text-sm text-gray-300 hover:bg-gray-600 hover:text-white transition-colors"
            >
              Change Plan
            </Link>
            <button
              onClick={handleManageSubscription}
              disabled={portalLoading}
              className="px-4 py-2 rounded-lg bg-gray-700 text-sm text-gray-300 hover:bg-gray-600 hover:text-white transition-colors disabled:opacity-50"
            >
              View Invoices
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
