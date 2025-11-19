import PropTypes from 'prop-types';
import React from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormLabel from 'Components/Form/FormLabel';
import FormInputGroup from 'Components/Form/FormInputGroup';
import { inputTypes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

function ProviderSelector(props) {
  const {
    searchMode,
    onSearchModeChange
  } = props;

  const searchModeOptions = [
    {
      key: 'reconciled',
      value: translate('ReconciledAllProviders')
    },
    {
      key: 'hardcover',
      value: 'Hardcover'
    },
    {
      key: 'openlibrary',
      value: 'Open Library'
    },
    {
      key: 'googlebooks',
      value: 'Google Books'
    },
    {
      key: 'comicvine',
      value: 'ComicVine'
    },
    {
      key: 'all',
      value: translate('AllProvidersGrouped')
    }
  ];

  return (
    <FormGroup>
      <FormLabel>
        {translate('MetadataProvider')}
      </FormLabel>

      <FormInputGroup
        type={inputTypes.SELECT}
        name="searchMode"
        value={searchMode}
        values={searchModeOptions}
        onChange={onSearchModeChange}
        helpText={translate('MetadataProviderHelpText')}
      />
    </FormGroup>
  );
}

ProviderSelector.propTypes = {
  searchMode: PropTypes.string.isRequired,
  onSearchModeChange: PropTypes.func.isRequired
};

export default ProviderSelector;
