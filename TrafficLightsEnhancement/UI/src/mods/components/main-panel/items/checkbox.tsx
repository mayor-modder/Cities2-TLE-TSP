import { useContext } from 'react';
import { Tooltip } from "cs2/ui"
import { trigger } from "cs2/api";
import { LocaleContext } from '../../../context';
import { getString } from '../../../localisations';
import mod from 'mod.json'
import styles from './itemsStyling.module.scss';
import Checkbox from '../../common/checkbox';

export interface MainPanelCheckboxProps {
  keyName: string;
  isChecked: boolean;
  label: string;
  triggerGroup?: string;
  triggerName: string;
  tooltip?: string;
  onClickOverride?: () => void;
  className?: string;
  disabled?: boolean;
}

export default function MainPanelCheckbox(props: MainPanelCheckboxProps) {
  const locale = useContext(LocaleContext);
  const triggerGroup = props.triggerGroup ?? mod.id;
  const triggerName = `TRIGGER:${props.triggerName}`;

  const clickHandler = () => {
    if (props.disabled) {
      return;
    }
    if (props.onClickOverride) {
      props.onClickOverride();
      return;
    }
    trigger(triggerGroup, triggerName, JSON.stringify({key: props.keyName, value: props.isChecked ? "false" : "true"}));
  };

  const content = (
    <div
      className={props.className}
      style={props.disabled ? { opacity: 0.5, cursor: "default" } : undefined}
    >
      <div
        className={styles.container}
        onClick={clickHandler}
        style={props.disabled ? { pointerEvents: "none" } : undefined}
      >
        <div className={styles.titleContainer}>
          <Checkbox isChecked={props.isChecked} />
          <div className={styles.label}>{getString(locale, props.label)}</div>
        </div>
      </div>
    </div>
    
  );

  return props.tooltip ? (
    <Tooltip direction="right" tooltip={props.tooltip}>
      {content}
    </Tooltip>
  ) : content;
}
