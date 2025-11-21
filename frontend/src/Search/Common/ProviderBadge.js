import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import { kinds } from 'Helpers/Props';
import styles from './ProviderBadge.css';

const providerConfig = {
  hardcover: {
    label: 'Hardcover',
    kind: kinds.INFO
  },
  openlibrary: {
    label: 'Open Library',
    kind: kinds.SUCCESS
  },
  googlebooks: {
    label: 'Google Books',
    kind: kinds.WARNING
  },
  comicvine: {
    label: 'ComicVine',
    kind: kinds.PRIMARY
  },
  goodreads: {
    label: 'Goodreads',
    kind: kinds.DANGER
  }
};

function ProviderBadge(props) {
  const {
    provider,
    matchedProviders,
    confidenceScore,
    showConfidence
  } = props;

  // Single provider mode
  if (provider && !matchedProviders) {
    const config = providerConfig[provider.toLowerCase()] || {
      label: provider,
      kind: kinds.DEFAULT
    };

    return (
      <Label
        kind={config.kind}
        className={styles.badge}
      >
        {config.label}
      </Label>
    );
  }

  // Reconciled mode - show matched providers
  if (matchedProviders && matchedProviders.length > 0) {
    return (
      <div className={styles.reconciledBadges}>
        {matchedProviders.map((prov) => {
          const config = providerConfig[prov.toLowerCase()] || {
            label: prov,
            kind: kinds.DEFAULT
          };

          return (
            <Label
              key={prov}
              kind={config.kind}
              className={styles.badge}
            >
              {config.label}
            </Label>
          );
        })}
        {showConfidence && confidenceScore != null && (
          <Label
            kind={kinds.PRIMARY}
            className={styles.confidenceBadge}
          >
            {`${Math.round(confidenceScore * 100)}% Match`}
          </Label>
        )}
      </div>
    );
  }

  return null;
}

ProviderBadge.propTypes = {
  provider: PropTypes.string,
  matchedProviders: PropTypes.arrayOf(PropTypes.string),
  confidenceScore: PropTypes.number,
  showConfidence: PropTypes.bool
};

ProviderBadge.defaultProps = {
  showConfidence: true
};

export default ProviderBadge;
